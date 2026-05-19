using System.Threading.Tasks;

namespace Nefarius.Utilities.TorProxy.Tools;

internal interface IFileFetcher
{
    Task<DownloadableFile> GetLatestAsync();
}
