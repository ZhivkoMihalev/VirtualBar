using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtualBar.Application.Interfaces;
using VirtualBar.Domain.Entities;
using VirtualBar.Infrastructure.Decorators;
using VirtualBar.Infrastructure.Options;
using VirtualBar.Infrastructure.Persistence;
using VirtualBar.Infrastructure.Services;

namespace VirtualBar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddScoped<AuthService>();
        services.AddScoped<IAuthService>(sp => new AuthValidationDecorator(
            sp.GetRequiredService<AuthService>(),
            sp.GetRequiredService<UserManager<AppUser>>(),
            sp.GetRequiredService<SignInManager<AppUser>>()));

        services.AddScoped<BottleService>();
        services.AddScoped<IBottleService>(sp => new BottleValidationDecorator(
            sp.GetRequiredService<BottleService>(),
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<ICurrentUser>()));

        services.AddScoped<BottleLikeService>();
        services.AddScoped<IBottleLikeService>(sp => new BottleLikeValidationDecorator(
            sp.GetRequiredService<BottleLikeService>(),
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<ICurrentUser>()));

        services.AddScoped<BottleCommentService>();
        services.AddScoped<IBottleCommentService>(sp => new BottleCommentValidationDecorator(
            sp.GetRequiredService<BottleCommentService>(),
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<ICurrentUser>()));

        services.AddScoped<UserFollowService>();
        services.AddScoped<IUserFollowService>(sp => new UserFollowValidationDecorator(
            sp.GetRequiredService<UserFollowService>(),
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<ICurrentUser>()));

        services.AddScoped<MessageService>();
        services.AddScoped<IMessageService>(sp => new MessageValidationDecorator(
            sp.GetRequiredService<MessageService>(),
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<ICurrentUser>()));

        services.AddScoped<BottleImageService>();
        services.AddScoped<IBottleImageService>(sp => new BottleImageValidationDecorator(
            sp.GetRequiredService<BottleImageService>(),
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<ICurrentUser>()));

        services.Configure<ProductLookupOptions>(
            configuration.GetSection(ProductLookupOptions.SectionName));

        services.AddHttpClient<ProductLookupService>();
        services.AddScoped<IProductLookupService>(sp =>
            new ProductValidationDecorator(sp.GetRequiredService<ProductLookupService>()));

        services.AddScoped<UserProfileService>();
        services.AddScoped<IUserProfileService>(sp => new UserProfileValidationDecorator(
            sp.GetRequiredService<UserProfileService>(),
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<ICurrentUser>()));

        services.AddScoped<NewsService>();
        services.AddScoped<INewsService>(sp => new NewsValidationDecorator(
            sp.GetRequiredService<NewsService>(),
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<ICurrentUser>()));

        services.AddScoped<FeedService>();
        services.AddScoped<IFeedService>(sp => new FeedValidationDecorator(
            sp.GetRequiredService<FeedService>()));

        return services;
    }
}
