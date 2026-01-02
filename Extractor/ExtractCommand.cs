using System.CommandLine;

namespace Extractor
{
    public class ExtractCommand : Command
    {
        public ExtractCommand() : base("extract", "Extract bank statements from PDF to JSON")
        {
            var inputOption = new Option<FileInfo>("--input", "-i")
            {
                Description = "Path to the input PDF file",
                Required = true
            };

            var outputOption = new Option<FileInfo>("--output", "-o")
            {
                Description = "Path to the output JSON file",
                Required = true
            };

            this.Options.Add(inputOption);
            this.Options.Add(outputOption);

            this.SetAction(async parsedOptions =>
            {
                var input = parsedOptions.GetRequiredValue(inputOption);
                var output = parsedOptions.GetRequiredValue(outputOption);

                if (!Path.IsPathRooted(input.ToString()))
                {
                    input = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), input.ToString()));
                }

                if (!Path.IsPathRooted(output.ToString()))
                {
                    output = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), output.ToString()));
                }

                Pdf.Extract(input, output);
            });
        }
    }
}
