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

        var allowOverride = new Option<bool>(
            aliases: ["--override"],
            description: "Allow overriding of the output file", 
            getDefaultValue: () => false);

        rootCommand.AddOption(allowOverride);

        rootCommand.SetHandler(async (context) =>
            returnCode = await MainAsync
            (
                context.ParseResult.GetValueForOption(infileName)!,
                context.ParseResult.GetValueForOption(outfileName)!,
                context.ParseResult.GetValueForOption(allowOverride)
            ));

        returnCode = await rootCommand.InvokeAsync(args);
        return returnCode;
    }
    private static async Task<int> MainAsync(string inFile, string outFile, bool allowOverride)
    {
        try
        {
            if (!allowOverride && File.Exists(outFile))
            {
                throw new ArgumentException(outFile + " already exists");
            }

            var fileContents = await File.ReadAllBytesAsync(inFile);

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(fileContents, QRCodeGenerator.ECCLevel.M);
            PngByteQRCode qrCode = new PngByteQRCode(qrData);
            byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);

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
