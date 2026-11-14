using FluentValidation;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application.UnitTests.Messaging;

public sealed class ValidationDecoratorTests
{
    [Fact]
    public async Task HandleAsync_WithInvalidCommand_ReturnsFieldErrorsWithoutCallingTheHandler()
    {
        var inner = new RecordingHandler();
        var decorator = new ValidationDecorator<SampleCommand, Result<int>>(inner, [new SampleValidator()]);

        var result = await decorator.HandleAsync(new SampleCommand(""), CancellationToken.None);

        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.Fields.ShouldNotBeNull();
        result.Error.Fields.ShouldContainKey(nameof(SampleCommand.Name));
        inner.WasCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithValidCommand_ReturnsTheHandlersResult()
    {
        var inner = new RecordingHandler();
        var decorator = new ValidationDecorator<SampleCommand, Result<int>>(inner, [new SampleValidator()]);

        var result = await decorator.HandleAsync(new SampleCommand("valid"), CancellationToken.None);

        result.Value.ShouldBe(42);
        inner.WasCalled.ShouldBeTrue();
    }

    public sealed record SampleCommand(string Name);

    private sealed class SampleValidator : AbstractValidator<SampleCommand>
    {
        public SampleValidator() => RuleFor(command => command.Name).NotEmpty();
    }

    private sealed class RecordingHandler : ICommandHandler<SampleCommand, Result<int>>
    {
        public bool WasCalled { get; private set; }

        public Task<Result<int>> HandleAsync(SampleCommand command, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult<Result<int>>(42);
        }
    }
}
