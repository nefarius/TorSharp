using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nefarius.Utilities.TorProxy.Tools;

internal class LineByLineConfigurer
{
    private readonly IConfigurationDictionary _configurationDictionary;
    private readonly IConfigurationFormat _format;

    public LineByLineConfigurer(IConfigurationDictionary configurationDictionary, IConfigurationFormat format)
    {
        _configurationDictionary = configurationDictionary;
        _format = format;
    }

    public async Task ApplySettings(Tool tool, TorProxySettings settings)
    {
        var dictionary = _configurationDictionary.GetDictionary(tool, settings);

        string? temporaryPath = null;
        try
        {
            temporaryPath = Path.GetTempFileName();
            TextReader reader;

            if (File.Exists(tool.ConfigurationPath))
            {
                reader = new StreamReader(new FileStream(tool.ConfigurationPath, FileMode.Open, FileAccess.Read, FileShare.None));
            }
            else
            {
                reader = new StringReader("# Full sample at https://github.com/torproject/tor/blob/master/src/config/torrc.sample.in");
            }

            using (reader)
            using (var writer = new StreamWriter(new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                string? originalLine;
                while ((originalLine = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    string? newLine = dictionary.Count > 0 ? _format.UpdateLine(dictionary, originalLine) : originalLine;
                    if (newLine != null)
                    {
                        await writer.WriteLineAsync(newLine).ConfigureAwait(false);
                    }
                }

                foreach (var pair in dictionary.OrderBy(p => p.Key))
                {
                    if (pair.Value != null && pair.Value.Any())
                    {
                        foreach (var value in pair.Value)
                        {
                            if (value != null)
                            {
                                string newLine = _format.CreateLine(new KeyValuePair<string, string>(pair.Key, value));
                                await writer.WriteLineAsync(newLine).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }

            if (File.Exists(tool.ConfigurationPath))
            {
                string backupPath = tool.ConfigurationPath + ".bak";

                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                File.Move(tool.ConfigurationPath, backupPath);
            }

            string? configurationDirectory = Path.GetDirectoryName(tool.ConfigurationPath);
            if (configurationDirectory != null && !Directory.Exists(configurationDirectory))
            {
                Directory.CreateDirectory(configurationDirectory);
            }

            File.Move(temporaryPath, tool.ConfigurationPath);
        }
        finally
        {
            if (temporaryPath != null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
