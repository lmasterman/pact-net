namespace PactNet
{
    public interface IMessageBuilderV3
    {
        IMessageBuilderV3 Given(string providerState);
        IMessageBuilderV3 WithMetadata(dynamic metadata);
        IMessageBuilderV3 WithContent(dynamic content);
    }
}
