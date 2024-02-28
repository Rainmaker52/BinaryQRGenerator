using System.Drawing;
using System.Text;

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Maui.Views;

using MimeDetective;
using MimeDetective.Storage;

using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.Windows.Compatibility;

using IImage = Microsoft.Maui.Graphics.IImage;

namespace BinaryQRCodeReader;

public partial class MainPage : ContentPage
{
    private string fileExtension = "*.*";
    private byte[]? bytesFromImage;
    private bool hasCamera = false;

    public MainPage()
    {
        InitializeComponent();
        cameraView.BarCodeOptions = new Camera.MAUI.ZXingHelper.BarcodeDecodeOptions()
        {
            ReadMultipleCodes = false,
            AutoRotate = true,
            TryInverted = true,
            PossibleFormats = [BarcodeFormat.QR_CODE],
            TryHarder = true
        };
        SaveBtn.IsEnabled = false;
    }

    private async void cameraView_CamerasLoaded(object sender, EventArgs e)
    {
        if(cameraView.Cameras.Count == 0)
        {
            hasCamera = false;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Still.IsEnabled = false;
                BarcodeInfo.Text = "No camera found";
            });
            return;
        }

        hasCamera = true;
        cameraView.Camera = cameraView.Cameras[0];
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await cameraView.StopCameraAsync();
            await cameraView.StartCameraAsync();
        });
    }

    private async void cameraView_BarcodeDetected(object sender, Camera.MAUI.ZXingHelper.BarcodeEventArgs args)
    {
        if (args.Result.Length == 0)
        {
            return;
        }
        await HandleQR(args.Result[0]);
    }

    private async void SaveBtn_Clicked(object sender, EventArgs e)
    {
        if(this.bytesFromImage == null)
        {
            return;
        }

        using var ms = new MemoryStream(this.bytesFromImage);
        var filesaverResult = await FileSaver.Default.SaveAsync("/media", $"Name.{this.fileExtension}", ms);
        if(filesaverResult.IsSuccessful)
        {
            Toast.Make("File saved successfully");
        }
        else
        {
            Toast.Make($"Error [{filesaverResult.Exception.Message}] occurred while saving file");
        }
    }

    private async Task HandleQR(Result scanResult)
    {
        var sb = new StringBuilder();
        sb.Append("Format: ");
        sb.Append(scanResult.BarcodeFormat);
        sb.Append('\n');

        foreach (var k in scanResult.ResultMetadata.Keys)
        {
            sb.Append(k.ToString());
            sb.Append(": ");
            sb.Append(scanResult.ResultMetadata[k].ToString());
            sb.Append('\n');
        }

        var byteArr = scanResult.ResultMetadata[ZXing.ResultMetadataType.BYTE_SEGMENTS] as List<byte[]>;

        if (byteArr == null || byteArr.Count == 0)
        {
            sb.Append("No byte array found\n");
            await MainThread.InvokeOnMainThreadAsync(() => BarcodeInfo.Text = sb.ToString());
            return;
        }

        this.bytesFromImage = byteArr[0];
        await MainThread.InvokeOnMainThreadAsync(() => SaveBtn.IsEnabled = true);

        sb.Append($"Byte arrays: {byteArr.Count}. Size {byteArr[0].LongLength} bytes\n");
        var inspector = new ContentInspectorBuilder()
        {
            Definitions = MimeDetective.Definitions.Default.All(),
            Parallel = false
        }.Build();
        
        var result = inspector.Inspect(this.bytesFromImage);
        var resultMime = new FileType();
        if (!result.Any())
        {
            sb.Append("Unknown MIME type");
            this.fileExtension = ".txt";
        }
        else
        {
            resultMime = result[0].Definition.File;
            this.fileExtension = result[0].Definition.File.Extensions[0];
            sb.Append($"MIME type {resultMime.MimeType}. {result[0].Points} points certainty\n");
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            BarcodeInfo.Text = sb.ToString();
        });

        // Generate the preview
        if (resultMime.Categories.Contains(MimeDetective.Storage.Category.Image))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                MediaPlayer.IsVisible = false;
                PreviewLabel.IsVisible = false;
                PreviewImage.IsVisible = true;
                PreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(byteArr[0]));
            });
        }
        else if (resultMime.Categories.Contains(MimeDetective.Storage.Category.Audio) || resultMime.Categories.Contains(MimeDetective.Storage.Category.Video))
        {
            var tempfileName = $"{FileSystem.CacheDirectory}/qr.{this.fileExtension}";
            await File.WriteAllBytesAsync(tempfileName, this.bytesFromImage);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PreviewImage.IsVisible = false;
                PreviewLabel.IsVisible = false;
                MediaPlayer.IsVisible = true;
                MediaPlayer.ShouldAutoPlay = true;
                MediaPlayer.Source = MediaSource.FromFile(tempfileName);
            });
        }
        else
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PreviewImage.IsVisible = false;
                MediaPlayer.IsVisible = false;
                PreviewLabel.IsVisible = true;

                PreviewLabel.Text = 
@$"No preview available. Please use the save button to save the file.
ASCII text: {Encoding.ASCII.GetString(this.bytesFromImage)}
UTF-8 text: {Encoding.UTF8.GetString(this.bytesFromImage)}";
            });
        }
    }

    private async void OpenFile_Clicked(object sender, EventArgs e)
    {
        if(this.hasCamera)
            await MainThread.InvokeOnMainThreadAsync(async () => await cameraView.StopCameraAsync());

        try
        {
            var selectedFile = await FilePicker.PickAsync(new PickOptions() { FileTypes = FilePickerFileType.Images });
            if (selectedFile == null)
                return;

            await MainThread.InvokeOnMainThreadAsync(() => SaveBtn.IsEnabled = false);
            await HandleFile(selectedFile.FullPath);
        } 
        finally
        {
            if(this.hasCamera)
                await MainThread.InvokeOnMainThreadAsync(async () => await cameraView.StartCameraAsync());
        }
    }

    private async void Still_Clicked(object sender, EventArgs e)
    {
        if (!hasCamera)
            return;

        Stream picture;
        picture = await cameraView.TakePhotoAsync(Camera.MAUI.ImageFormat.PNG);
        
        if(picture == null)
        {
            await MainThread.InvokeOnMainThreadAsync(() => BarcodeInfo.Text = "Failed to take picture");
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(async () => await cameraView.StopCameraAsync());
        try
        {
            var tempfileName = $"{FileSystem.CacheDirectory}/qr.png";
            var fileStream = File.Create(tempfileName);

            await picture.CopyToAsync(fileStream);
            fileStream.Close();

            await HandleFile(tempfileName);
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(async () => await cameraView.StartCameraAsync());
        }
    }

    private async Task HandleFile(string fullPath)
    {
        try
        {
            using var image = (Bitmap)Bitmap.FromFile(fullPath);

            LuminanceSource source;
            source = new BitmapLuminanceSource(image);
            BinaryBitmap bitmap = new BinaryBitmap(new HybridBinarizer(source));
            Result result = new MultiFormatReader().decode(bitmap);
            if (result != null)
            {
                await HandleQR(result);
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() => BarcodeInfo.Text = "No QR code found");
            }
        }
        catch (Exception exception)
        {
            await MainThread.InvokeOnMainThreadAsync(() => BarcodeInfo.Text = "Cannot decode the QR code " + exception.Message);
        }

    }
}

