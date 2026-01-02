using System.CommandLine;

namespace Extractor
{
    public class AnalyzeCommand : Command
    {
        public AnalyzeCommand() : base("analyze", "Analyze bank statements in PDF format")
        {
            var inputOption = new Option<FileInfo>("--input", "-i")
            {
                Description = "Path to the input PDF file",
                Required = true
            };

            this.Options.Add(inputOption);

            this.SetAction(async parsedOptions =>
            {
                var input = parsedOptions.GetRequiredValue(inputOption);

                if (!Path.IsPathRooted(input.ToString()))
                {
                    input = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), input.ToString()));
                }

                Pdf.AnalyzeStructure(input);
            });
        }
    }
}
