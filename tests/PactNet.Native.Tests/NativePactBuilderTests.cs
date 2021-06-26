using System;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Moq;
using PactNet.Infrastructure.Outputters;
using Xunit;

namespace PactNet.Native.Tests
{
    public class NativePactBuilderTests
    {
        private readonly NativePactBuilder builder;

        private readonly Mock<IMockServer> mockServer;
        private readonly Mock<IOutput> mockOutput;

        private readonly IFixture fixture;
        private readonly PactHandle handle;
        private readonly Uri serverUri;
        private readonly PactConfig config;

        public NativePactBuilderTests()
        {
            this.mockServer = new Mock<IMockServer>(MockBehavior.Strict);
            this.mockOutput = new Mock<IOutput>();

            this.fixture = new Fixture();
            var customization = new SupportMutableValueTypesCustomization();
            customization.Customize(this.fixture);

            this.handle = this.fixture.Create<PactHandle>();
            this.serverUri = this.fixture.Create<Uri>();
            this.config = new PactConfig
            {
                Outputters = new[] { this.mockOutput.Object }
            };

            // set some default mock setups
            this.mockServer.Setup(s => s.CreateMockServerForPact(this.handle, "127.0.0.1:0", false)).Returns(this.serverUri.Port);
            this.mockServer.Setup(s => s.NewInteraction(this.handle, It.IsAny<string>())).Returns(new InteractionHandle());
            this.mockServer.Setup(s => s.MockServerLogs(this.serverUri.Port)).Returns(string.Empty);
            this.mockServer.Setup(s => s.MockServerMismatches(this.serverUri.Port)).Returns(string.Empty);
            this.mockServer.Setup(s => s.WritePactFile(this.serverUri.Port, this.config.PactDir, false));
            this.mockServer.Setup(s => s.CleanupMockServer(this.serverUri.Port)).Returns(true);

            this.builder = new NativePactBuilder(this.mockServer.Object, this.handle, this.config);
        }

        [Fact]
        public void UponReceiving_WhenCalled_CreatesNewInteraction()
        {
            this.builder.UponReceiving("test description");

            this.mockServer.Verify(s => s.NewInteraction(this.handle, "test description"));
        }

        [Fact]
        public void Verify_WhenCalled_StartsMockServer()
        {
            this.builder.Verify(Success);

            this.mockServer.Verify(s => s.CreateMockServerForPact(this.handle, "127.0.0.1:0", false));
        }

        [Fact]
        public void Verify_ErrorStartingMockServer_ThrowsInvalidOperationException()
        {
            this.mockServer
                .Setup(s => s.CreateMockServerForPact(It.IsAny<PactHandle>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Throws<InvalidOperationException>();

            Action action = () => this.builder.Verify(Success);

            action.Should().Throw<InvalidOperationException>("because the mock server failed to start");
        }

        [Fact(Skip = "Enable this when the released FFI library supports getting mock server logs")]
        public void Verify_NoLogs_DoesNotWriteLogs()
        {
            this.builder.Verify(Success);

            this.mockOutput.Verify(o => o.WriteLine(It.IsAny<string>()), Times.Never);
        }

        [Fact(Skip = "Enable this when the released FFI library supports getting mock server logs")]
        public void Verify_Logs_WritesLogsToOutput()
        {
            this.mockServer.Setup(s => s.MockServerLogs(this.serverUri.Port)).Returns("some logs");

            this.builder.Verify(Success);

            this.mockOutput.Verify(o => o.WriteLine("some logs"));
        }

        [Fact]
        public void Verify_InteractionThrowsException_RethrowsInteractionException()
        {
            this.builder
                .Invoking(b => b.Verify(Error))
                .Should().ThrowExactly<Exception>("because the inner interaction call failed");
        }

        [Fact]
        public void Verify_NoMismatches_WritesPactFile()
        {
            this.builder.Verify(Success);

            this.mockServer.Verify(s => s.WritePactFile(this.serverUri.Port, this.config.PactDir, false));
        }

        [Fact]
        public void Verify_NoMismatches_ShutsDownMockServer()
        {
            this.builder.Verify(Success);

            this.mockServer.Verify(s => s.CleanupMockServer(this.serverUri.Port));
        }

        [Fact]
        public void Verify_FailedToWritePactFile_ThrowsInvalidOperationException()
        {
            this.mockServer
                .Setup(s => s.WritePactFile(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Throws<InvalidOperationException>();

            Action action = () => this.builder.Verify(Success);

            action.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Verify_Mismatches_DoesNotWritePactFile()
        {
            this.mockServer.Setup(s => s.MockServerMismatches(this.serverUri.Port)).Returns("some mismatches");

            try
            {
                this.builder.Verify(Success);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }

            this.mockServer.Verify(s => s.WritePactFile(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public void Verify_Mismatches_WritesMismatchesToOutput()
        {
            this.mockServer.Setup(s => s.MockServerMismatches(this.serverUri.Port)).Returns("some mismatches");

            try
            {
                this.builder.Verify(Success);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }

            this.mockOutput.Verify(o => o.WriteLine("some mismatches"));
        }

        [Fact]
        public void Verify_Mismatches_ThrowsPactFailureException()
        {
            this.mockServer.Setup(s => s.MockServerMismatches(this.serverUri.Port)).Returns("some mismatches");

            Action action = () => this.builder.Verify(Success);

            action.Should().Throw<PactFailureException>();
        }

        [Fact]
        public void Verify_Mismatches_ShutsDownMockServer()
        {
            this.mockServer.Setup(s => s.MockServerMismatches(this.serverUri.Port)).Returns("some mismatches");

            try
            {
                this.builder.Verify(Success);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }

            this.mockServer.Verify(s => s.CleanupMockServer(this.serverUri.Port));
        }

        private static void Success(IConsumerContext ctx)
        {
        }

        private static void Error(IConsumerContext ctx)
        {
            throw new Exception("Interaction failed");
        }
    }
}
