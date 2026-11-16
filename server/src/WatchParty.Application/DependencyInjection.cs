using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WatchParty.Application.Abstractions.Events;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.History.Commands.MarkRead;
using WatchParty.Application.History.Commands.RecordAttendance;
using WatchParty.Application.History.Commands.RemoveFromHistory;
using WatchParty.Application.History.Commands.SetFavourite;
using WatchParty.Application.History.Services;
using WatchParty.Application.Identity.Login;
using WatchParty.Application.Identity.Queries;
using WatchParty.Application.Identity.SignUp;
using WatchParty.Application.Realtime;
using WatchParty.Application.Rooms.Commands.CreateRoom;
using WatchParty.Application.Rooms.Commands.Heartbeat;
using WatchParty.Application.Rooms.Commands.JoinRoom;
using WatchParty.Application.Rooms.Commands.LeaveRoom;
using WatchParty.Application.Rooms.Commands.RecordPlayback;
using WatchParty.Application.Rooms.Commands.RepointVideo;
using WatchParty.Application.Rooms.Commands.TouchRoom;
using WatchParty.Application.Rooms.Commands.TransferOwnership;
using WatchParty.Application.Rooms.Commands.PostMessage;
using WatchParty.Application.Rooms.EventHandlers;
using WatchParty.Application.Rooms.Services;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Singleton, includeInternalTypes: true);

        // Shared building blocks the handlers below are made of.
        services.AddScoped<RoomSuccession>();
        services.AddScoped<RoomEntry>();
        services.AddScoped<RoomEditor>();
        services.AddScoped<HistoryEditor>();

        services.AddCommand<LoginCommand, Result<LoginResult>, LoginHandler>();
        services.AddCommand<SignUpCommand, Result<LoginResult>, SignUpHandler>();

        services.AddCommand<CreateRoomCommand, Result<RoomId>, CreateRoomHandler>();
        services.AddCommand<RecordPlaybackCommand, Result, RecordPlaybackHandler>();
        services.AddCommand<TouchRoomCommand, Result, TouchRoomHandler>();
        services.AddCommand<RepointVideoCommand, Result, RepointVideoHandler>();
        services.AddCommand<TransferOwnershipCommand, Result, TransferOwnershipHandler>();
        services.AddCommand<JoinRoomCommand, Result<JoinResult>, JoinRoomHandler>();
        services.AddCommand<HeartbeatCommand, Result, HeartbeatHandler>();
        services.AddCommand<LeaveRoomCommand, Result, LeaveRoomHandler>();
        services.AddCommand<PostMessageCommand, Result<ChatMessageId>, PostMessageHandler>();

        services.AddCommand<SetFavouriteCommand, Result, SetFavouriteHandler>();
        services.AddCommand<MarkReadCommand, Result, MarkReadHandler>();
        services.AddCommand<RemoveFromHistoryCommand, Result, RemoveFromHistoryHandler>();
        services.AddCommand<RecordAttendanceCommand, Result, RecordAttendanceHandler>();

        // Each class is registered for every IDomainEventHandler<TEvent> it implements, so
        // reacting to a new event means adding the interface to the class, nothing here.
        services.AddEventHandlers<RealtimeNotifications>();
        services.AddEventHandlers<RecordAttendanceOnJoin>();
        services.AddEventHandlers<SyncMemberRolesOnOwnershipChange>();
        return services;
    }

    /// <summary>Registers a handler behind the validation decorator.</summary>
    private static void AddCommand<TCommand, TResult, THandler>(this IServiceCollection services)
        where TResult : Result
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        services.AddScoped<THandler>();
        services.AddScoped<ICommandHandler<TCommand, TResult>>(serviceProvider =>
            new ValidationDecorator<TCommand, TResult>(
                serviceProvider.GetRequiredService<THandler>(),
                serviceProvider.GetServices<IValidator<TCommand>>()));
    }

    /// <summary>
    /// Registers <typeparamref name="THandler"/> once, and as the handler for each event it
    /// declares through <see cref="IDomainEventHandler{TEvent}"/>. Both resolve to the same
    /// instance within a scope.
    /// </summary>
    private static void AddEventHandlers<THandler>(this IServiceCollection services)
        where THandler : class
    {
        services.AddScoped<THandler>();

        var handlerInterfaces = typeof(THandler).GetInterfaces()
            .Where(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>));

        foreach (var handlerInterface in handlerInterfaces)
        {
            services.AddScoped(handlerInterface, serviceProvider => serviceProvider.GetRequiredService<THandler>());
        }
    }
}
