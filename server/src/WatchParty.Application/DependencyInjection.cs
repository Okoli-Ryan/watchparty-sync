using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using WatchParty.Application.Abstractions.Events;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.History.Commands;
using WatchParty.Application.Identity.CreateUser;
using WatchParty.Application.Identity.Login;
using WatchParty.Application.Identity.Queries;
using WatchParty.Application.Identity.SeedAdmin;
using WatchParty.Application.Realtime;
using WatchParty.Application.Rooms.Commands.CreateRoom;
using WatchParty.Application.Rooms.Commands.HostActions;
using WatchParty.Application.Rooms.Commands.PostMessage;
using WatchParty.Application.Rooms.Commands.Presence;
using WatchParty.Application.Rooms.EventHandlers;
using WatchParty.Application.Rooms.Services;
using WatchParty.Domain.History;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Rooms.Events;
using WatchParty.Domain.SharedKernel;

namespace WatchParty.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Singleton, includeInternalTypes: true);
        services.AddScoped<UserRegistration>();
        services.AddScoped<RoomSuccession>();

        services.AddCommand<LoginCommand, Result<LoginResult>, LoginHandler>();
        services.AddCommand<CreateUserCommand, Result<UserProfileDto>, CreateUserHandler>();
        services.AddCommand<SeedAdminCommand, Result<UserProfileDto>, SeedAdminHandler>();

        services.AddCommand<CreateRoomCommand, Result<RoomId>, CreateRoomHandler>();
        services.AddCommand<RecordPlaybackCommand, Result, HostActionHandlers>();
        services.AddCommand<TouchRoomCommand, Result, HostActionHandlers>();
        services.AddCommand<RepointVideoCommand, Result, HostActionHandlers>();
        services.AddCommand<TransferOwnershipCommand, Result, HostActionHandlers>();
        services.AddCommand<JoinRoomCommand, Result<JoinResult>, PresenceHandlers>();
        services.AddCommand<HeartbeatCommand, Result, PresenceHandlers>();
        services.AddCommand<LeaveRoomCommand, Result, PresenceHandlers>();
        services.AddCommand<PostMessageCommand, Result<ChatMessageId>, PostMessageHandler>();

        services.AddCommand<SetFavouriteCommand, Result, HistoryHandlers>();
        services.AddCommand<MarkReadCommand, Result, HistoryHandlers>();
        services.AddCommand<RemoveFromHistoryCommand, Result, HistoryHandlers>();
        services.AddCommand<RecordAttendanceCommand, Result, HistoryHandlers>();

        services.AddScoped<RealtimeNotifications>();
        services.AddEventHandler<RoomCreated, RealtimeNotifications>();
        services.AddEventHandler<PlaybackRecorded, RealtimeNotifications>();
        services.AddEventHandler<RoomTouched, RealtimeNotifications>();
        services.AddEventHandler<RoomVideoRepointed, RealtimeNotifications>();
        services.AddEventHandler<RoomOwnershipChanged, RealtimeNotifications>();
        services.AddEventHandler<RoomDeactivated, RealtimeNotifications>();
        services.AddEventHandler<MemberJoined, RealtimeNotifications>();
        services.AddEventHandler<MemberHeartbeatRecorded, RealtimeNotifications>();
        services.AddEventHandler<MemberRoleChanged, RealtimeNotifications>();
        services.AddEventHandler<MemberLeft, RealtimeNotifications>();
        services.AddEventHandler<ChatMessagePosted, RealtimeNotifications>();
        services.AddEventHandler<HistoryEntryChanged, RealtimeNotifications>();
        services.AddEventHandler<HistoryEntryRemoved, RealtimeNotifications>();

        services.AddScoped<IDomainEventHandler<MemberJoined>, RecordAttendanceOnJoin>();
        services.AddScoped<IDomainEventHandler<RoomOwnershipChanged>, SyncMemberRolesOnOwnershipChange>();
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

    private static void AddEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IDomainEvent
        where THandler : class, IDomainEventHandler<TEvent> =>
        services.AddScoped<IDomainEventHandler<TEvent>>(serviceProvider => serviceProvider.GetRequiredService<THandler>());
}
