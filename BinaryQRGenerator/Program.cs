using System;
using System.CommandLine;

using QRCoder;

namespace BinaryQRGenerator;

internal static class Program
{
    static async Task<int> Main(string[] args)
    {
        var returnCode = 0;
        var rootCommand = new RootCommand("Encodes a file to a QR code. Will depend on the QR scanner on how this is interpreted");

        var infileName = new Option<string>(
            aliases: ["--input", "-i"],
            description: "The file to encode")
        {
            IsRequired = true
        };
        rootCommand.AddOption(infileName);

        var outfileName = new Option<string>(
            aliases: ["--output", "-o"],
            description: "The output file")
        {
            IsRequired = true
        };
        rootCommand.AddOption(outfileName);

        var allowOverwrite = new Option<bool>(
            aliases: ["--overwrite"],
            description: "Allow overwrite of the output file", 
            getDefaultValue: () => false);
        rootCommand.AddOption(allowOverwrite);

        var pixelsPerModule = new Option<int>(
            aliases: ["--pixels", "-p"],
            description: "The number of pixels per module. Higher values lead to bigger pictures. Note that your scanner (camera / phone) will need to have at least the resolution to distinguish between the pixels.",
            getDefaultValue: () => 38);
        rootCommand.AddOption(pixelsPerModule);

        var errorCorrecting = new Option<QRCodeGenerator.ECCLevel>(
            aliases: ["--ecc"],
            description: "The error correcting level to use\nL (Low) = 7% of image can be damaged. Maximum size 2953 bytes.\nM (Medium, default) = 15% of image can be damaged. Maximum size 2331 bytes\nQ (Quartile) = 25% of image can be damaged. Maximum size 1663 bytes\nH (High) = 30% of image can be damaged. Maximum size 1273 bytes",
            getDefaultValue: () => QRCodeGenerator.ECCLevel.M);
        rootCommand.AddOption(errorCorrecting);

        rootCommand.SetHandler(async (context) =>
            returnCode = await MainAsync
            (
                context.ParseResult.GetValueForOption(infileName)!,
                context.ParseResult.GetValueForOption(outfileName)!,
                context.ParseResult.GetValueForOption(allowOverwrite),
                context.ParseResult.GetValueForOption(errorCorrecting),
                context.ParseResult.GetValueForOption(pixelsPerModule)
            ));

        returnCode = await rootCommand.InvokeAsync(args);
        return returnCode;
    }
    private static async Task<int> MainAsync(string inFile, string outFile, bool allowOverwrite, QRCodeGenerator.ECCLevel eccLevel, int pixelsPerModule)
    {
        try
        {
            if (!allowOverwrite && File.Exists(outFile))
            {
                throw new ArgumentException(outFile + " already exists. Use --overwrite to allow overwriting");
            }

            var fileContents = await File.ReadAllBytesAsync(inFile);

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(fileContents, eccLevel);
            
            PngByteQRCode qrCode = new PngByteQRCode(qrData);
            byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(pixelsPerModule);

            await File.WriteAllBytesAsync(outFile, qrCodeAsPngByteArr);

            return 0;
        }
        catch(Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
            return 1;
        }


    }
}
