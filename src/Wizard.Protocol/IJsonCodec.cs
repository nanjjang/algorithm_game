namespace Wizard.Protocol
{
    // CONTRACTS 6.2, T-20. 서버는 System.Text.Json, 클라이언트는 Newtonsoft 구현. 픽스처 왕복 테스트로 묶는다
    public interface IJsonCodec
    {
        string Serialize<T>(T value);
        bool TryDeserialize<T>(string json, out T value);
    }
}
