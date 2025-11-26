namespace base64reducer;

using System;
using System.CommandLine;
using System.IO;

internal static class Program
{
    public static int Main(string[] args)
    {
        var rootCommand = new RootCommand("Optimizes an image (file or stdin) to Base64/binary with size limits.");

        var inputArg = new Argument<string>("input")
        {
            Description = "Input file path or '-' for stdin"
        };
        var maxBase64Opt = new Option<int?>("--max-base64")
        {
            DefaultValueFactory = (parseResult) => default,
            Description = "Maximum Base64 string length (e.g. 32768)"
        };
        var maxBytesOpt = new Option<int?>("--max-bytes")
        {
            DefaultValueFactory = (parseResult) => default,
            Description = "Maximum binary size in bytes (e.g. 24576)"
        };
        var formatOpt = new Option<TargetFormat>("--format")
        {
            DefaultValueFactory = (parseResult) => TargetFormat.WebP,
            Description = "Output format: Jpeg or WebP"
        };
        var maxSizeOpt = new Option<int>("--max-size")
        {
            DefaultValueFactory = (parseResult) => 0,
            Description = "Max width/height in pixels (0 = no resize)"
        };
        var qualityOpt = new Option<int>("--quality")
        {
            DefaultValueFactory = (parseResult) => 90,
            Description = "Initial quality (1–100). Reduced if needed"
        };
        var minQualityOpt = new Option<int>("--min-quality")
        {
            DefaultValueFactory = (parseResult) => 30,
            Description = "Minimum quality to try"
        };
        var binaryOpt = new Option<bool>("--binary")
        {
            DefaultValueFactory = (parseResult) => false,
            Description = "Output raw binary (not Base64)"
        };
        var dataUriOpt = new Option<bool>("--data-uri")
        {
            DefaultValueFactory = (parseResult) => false,
            Description = "Output full  URI"
        };
        var outputOpt = new Option<string?>("--output")
        {
            DefaultValueFactory = (parseResult) => default,
            Description = "Output file path (omit for stdout)"
        };

        // Добавляем — работает в 2.0.0
        rootCommand.Add(inputArg);
        rootCommand.Add(maxBase64Opt);
        rootCommand.Add(maxBytesOpt);
        rootCommand.Add(formatOpt);
        rootCommand.Add(maxSizeOpt);
        rootCommand.Add(qualityOpt);
        rootCommand.Add(minQualityOpt);
        rootCommand.Add(binaryOpt);
        rootCommand.Add(dataUriOpt);
        rootCommand.Add(outputOpt);

        // Обработчик — синхронный, через SetHandler
        rootCommand.SetAction(parseResult =>
        {
               var (input, maxBase64, maxBytes, format, maxSize, quality, minQuality, binary, dataUri, output) =
                (parseResult.GetValue(inputArg), parseResult.GetValue(maxBase64Opt),
                parseResult.GetValue(maxBytesOpt), parseResult.GetValue(formatOpt),
                parseResult.GetValue(maxSizeOpt), parseResult.GetValue(qualityOpt),
                parseResult.GetValue(minQualityOpt), parseResult.GetValue(binaryOpt),
                parseResult.GetValue(dataUriOpt), parseResult.GetValue(outputOpt));

                Stream inputStream;
                string inputDesc;

                if (input == "-")
                {
                    inputStream = Console.OpenStandardInput();
                    inputDesc = "<stdin>";
                }
                else
                {
                    if (!File.Exists(input))
                    {
                        Console.Error.WriteLine($"Error: Input file not found: {input}");
                        return 1;
                    }
                    inputStream = File.OpenRead(input);
                    inputDesc = Path.GetFileName(input);
                }

                if (!maxBase64.HasValue && !maxBytes.HasValue)
                {
                    Console.Error.WriteLine("Error: Specify at least --max-base64 or --max-bytes.");
                    inputStream?.Dispose();
                    return 1;
                }

                try
                {
                    string resultText = "";
                    byte[]? resultBytes = null;

                    using (inputStream)
                    {
                        if (binary)
                        {
                            resultBytes = ImageBase64Optimizer.OptimizeToBytes(
                                inputStream, maxBase64, maxBytes, format, maxSize, quality, minQuality);
                        }
                        else if (dataUri)
                        {
                            resultText = ImageBase64Optimizer.OptimizeToDataUri(
                                inputStream, maxBase64, maxBytes, format, maxSize, quality, minQuality);
                        }
                        else
                        {
                            resultText = ImageBase64Optimizer.OptimizeToBase64(
                                inputStream, maxBase64, maxBytes, format, maxSize, quality, minQuality);
                        }
                    }

                    if (!string.IsNullOrEmpty(output))
                    {
                        string? dir = Path.GetDirectoryName(output);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        if (binary && resultBytes is not null)
                        {
                            File.WriteAllBytes(output, resultBytes);
                        }
                        else
                        {
                            File.WriteAllText(output, resultText);
                        }

                        Console.WriteLine($"OK '{inputDesc}' → '{output}'");
                        if (binary && resultBytes is not null)
                            Console.WriteLine($"* Binary size: {resultBytes.Length} bytes");
                        else
                            Console.WriteLine($"* Base64 length: {resultText.Length} chars (~{(resultText.Length * 3 / 4)} B)");
                    }
                    else
                    {
                        if (binary && resultBytes is not null)
                        {
                            using var stdout = Console.OpenStandardOutput();
                            stdout.Write(resultBytes, 0, resultBytes.Length);
                            stdout.Flush();
                        }
                        else
                        {
                            Console.WriteLine(resultText);
                        }
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
                    return 1;
                }
            }
        );

        return rootCommand.Parse(args).Invoke();
    }
}
