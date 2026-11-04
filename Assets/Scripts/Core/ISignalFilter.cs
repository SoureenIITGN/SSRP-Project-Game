namespace SSRP.Core
{
    public interface ISignalFilter
    {
        float Process(float x, float dt);
        void Reset();
    }
}