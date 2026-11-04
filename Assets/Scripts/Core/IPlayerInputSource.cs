namespace SSRP.Core
{
    public interface IPlayerInputSource
    {
        bool IsConnected { get; }

        bool TryGetLatest(
            int playerId,
            out RawFsrSample sample
        );
    }
}