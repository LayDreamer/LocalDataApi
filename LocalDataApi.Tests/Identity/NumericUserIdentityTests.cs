using System.Security.Claims;
using LocalDataApi.Application.Identity;
using LocalDataApi.Domain.Employee;
using LocalDataApi.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LocalDataApi.Tests.Identity;

public sealed class NumericUserIdentityTests
{
    [Fact]
    public void CurrentUser_ParsesNumericNameIdentifier()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "42"),
                new Claim(ClaimTypes.Name, "admin"),
                new Claim("sid", Guid.NewGuid().ToString())
            ], "test"))
        };
        var accessor = new HttpContextAccessor { HttpContext = context };

        var currentUser = new CurrentUserService(accessor);

        Assert.Equal(42L, currentUser.UserId);
        Assert.Equal("admin", currentUser.UserName);
    }

    [Theory]
    [InlineData("legacy-guid-user-id")]
    [InlineData("0")]
    public void CurrentUser_RejectsNonPlatformUserIdentifiers(string value)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, value)], "test"))
        };

        var currentUser = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        Assert.Null(currentUser.UserId);
    }

    [Fact]
    public void AccountAggregate_UsesLongPlatformKeys()
    {
        Assert.Equal(typeof(long), typeof(User).GetProperty(nameof(User.Id))!.PropertyType);
        Assert.Equal(typeof(long?), typeof(Employee).GetProperty(nameof(Employee.UserId))!.PropertyType);
        Assert.Equal(typeof(long), typeof(UserExternalIdentity).GetProperty(nameof(UserExternalIdentity.UserId))!.PropertyType);
        Assert.Equal(typeof(long), typeof(UserLegacyMap).GetProperty(nameof(UserLegacyMap.UserId))!.PropertyType);
    }
}
