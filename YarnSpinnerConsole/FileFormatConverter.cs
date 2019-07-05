using System;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

namespace Yarn
{
    public static class FileFormatConverter
    {
        static internal int ConvertFormat(ConvertFormatOptions options)
        {

            YarnSpinnerConsole.CheckFileList(options.files, YarnSpinnerConsole.ALLOWED_EXTENSIONS);

            if (options.convertToJSON)
            {
                return ConvertToJSON(options);
            }
            if (options.convertToYarn)
            {
                return ConvertToYarn(options);
            }

            var processName = System.IO.Path.GetFileName(Environment.GetCommandLineArgs()[0]);

            YarnSpinnerConsole.Error(string.Format(CultureInfo.CurrentCulture, "You must specify a destination format. Run '{0} help convert' to learn more.", processName));
            return 1;
        }

        static int ConvertToJSON(ConvertFormatOptions options)
        {
            foreach (var file in options.files)
            {

                if (Loader.GetFormatFromFileName(file) == NodeFormat.JSON)
                {
                    YarnSpinnerConsole.Warn(string.Format(CultureInfo.CurrentCulture, "Not converting file {0}, because its name implies it's already in JSON format", file));
                    continue;
                }

                ConvertNodesInFile(options, file, "json", (IEnumerable<Loader.NodeInfo> nodes) => JsonConvert.SerializeObject(nodes, Formatting.Indented));

            }
            return 0;
        }

        static int ConvertToYarn(ConvertFormatOptions options)
        {
            foreach (var file in options.files)
            {
                if (Loader.GetFormatFromFileName(file) == NodeFormat.Text)
                {
                    YarnSpinnerConsole.Warn(string.Format(CultureInfo.CurrentCulture, "Not converting file {0}, because its name implies it's already in Yarn format", file));
                    continue;
                }

                ConvertNodesInFile(options, file, "yarn.txt", ConvertNodesToYarnText);
            }
            return 0;
        }

        internal static string ConvertNodes(IEnumerable<Loader.NodeInfo> nodes, NodeFormat format) {
            switch (format)
            {
                case NodeFormat.JSON:
                    return JsonConvert.SerializeObject(nodes, Formatting.Indented);
                case NodeFormat.Text:
                    return ConvertNodesToYarnText(nodes);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        static string ConvertNodesToYarnText(IEnumerable<Loader.NodeInfo> nodes)
        {
            var sb = new System.Text.StringBuilder();

            var properties = typeof(Loader.NodeInfo).GetProperties();

            foreach (var node in nodes) {

                foreach (var property in properties) {

                    // ignore the body attribute
                    if (property.Name == "body") {
                        continue;
                    }

                    // piggy-back off the JsonIgnoreAttribute to sense items that should not be serialised
                    if (property.GetCustomAttributes(typeof(JsonIgnoreAttribute), false).Length > 0) {
                        continue;
                    }

                    var field = property.Name;

                    string value;

                    var propertyType = property.PropertyType;
                    if (propertyType.IsAssignableFrom(typeof(string)))
                    {
                        value = (string)property.GetValue(node, null);

                        // avoid storing nulls when we could store the empty string instead
                        if (value == null)
                            value = "";
                    }
                    else if (propertyType.IsAssignableFrom(typeof(int)))
                    {
                        value = ((int)property.GetValue(node, null)).ToString(CultureInfo.InvariantCulture);
                    }
                    else if (propertyType.IsAssignableFrom(typeof(Loader.NodeInfo.Position)))
                    {
                        var position = (Loader.NodeInfo.Position)property.GetValue(node, null);

                        value = string.Format(CultureInfo.InvariantCulture, "{0},{1}", position.x, position.y);
                    } else {
                        YarnSpinnerConsole.Error(string.Format(CultureInfo.CurrentCulture, "Internal error: Node {0}'s property {1} has unsupported type {2}", node.title, property.Name, propertyType.FullName));

                        // will never be run, but prevents the compiler being mean about us not returning a value
                        throw new Exception();
                    }

                    var header = string.Format(CultureInfo.InvariantCulture, "{0}: {1}", field, value);

                    sb.AppendLine(header);

                }
                // now write the body
                sb.AppendLine("---");

                sb.AppendLine(node.body);

                sb.AppendLine("===");

            }

            return sb.ToString();
        }


        delegate string ConvertNodesToText(IEnumerable<Loader.NodeInfo> nodes);

        static void ConvertNodesInFile(ConvertFormatOptions options, string file, string fileExtension, ConvertNodesToText convert)
        {
            var d = new Dialogue(null);

            var text = File.ReadAllText(file);

            IEnumerable<Loader.NodeInfo> nodes;
            try {
                nodes = d.loader.GetNodesFromText(text, Loader.GetFormatFromFileName(file));
            } catch (FormatException e) {
                YarnSpinnerConsole.Error(e.Message);
                return;
            }

            var serialisedText = convert(nodes);

            var destinationDirectory = options.outputDirectory;

            if (destinationDirectory == null)
            {
                destinationDirectory = Path.GetDirectoryName(file);
            }

            var fileName = Path.GetFileName(file);

            // ChangeExtension thinks that the file "Foo.yarn.txt" has the extension "txt", so
            // to simplify things, just lop that extension off right away if it's there
            fileName = fileName.Replace(".yarn.txt", "");

            // change the filename's extension
            fileName = Path.ChangeExtension(fileName, fileExtension);

            // figure out where we're writing this file
            var destinationFilePath = Path.Combine(destinationDirectory, fileName);

            File.WriteAllText(destinationFilePath, serialisedText);

            if (options.verbose)
            {
                YarnSpinnerConsole.Note("Wrote " + destinationFilePath);
            }
        }


    }
}

