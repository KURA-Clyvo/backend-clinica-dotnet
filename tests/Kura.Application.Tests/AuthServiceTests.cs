using FluentAssertions;
using Kura.Application.DTOs.Auth;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Kura.Application.Tests;

public class AuthServiceTests
{
    private readonly Mock<IRepository<Clinica>> _repoMock = new();
    private readonly IConfiguration _config;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "supersecretkey12345678901234567890123456789012",
            ["Jwt:Issuer"] = "kura-api",
            ["Jwt:Audience"] = "kura-client",
            ["Jwt:ExpiryHours"] = "8"
        };
        _config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        _sut = new AuthService(_repoMock.Object, _config);
    }

    [Fact]
    public async Task LoginAsync_EmailNotFound_ThrowsRegraDeNegocio()
    {
        _repoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Clinica, bool>>>()))
            .ReturnsAsync(Enumerable.Empty<Clinica>());

        var act = () => _sut.LoginAsync(new LoginDto { DsEmail = "x@x.com", DsSenha = "pass" });

        await act.Should().ThrowAsync<RegraDeNegocioException>()
            .WithMessage("Email ou senha inválidos.");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsRegraDeNegocio()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("correct");
        var clinica = new Clinica { Id = 1, DsEmail = "a@a.com", DsSenha = hash, StAtiva = 'S' };

        _repoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Clinica, bool>>>()))
            .ReturnsAsync(new[] { clinica });

        var act = () => _sut.LoginAsync(new LoginDto { DsEmail = "a@a.com", DsSenha = "wrong" });

        await act.Should().ThrowAsync<RegraDeNegocioException>()
            .WithMessage("Email ou senha inválidos.");
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("secret");
        var clinica = new Clinica { Id = 5, DsEmail = "vet@clinic.com", DsSenha = hash, StAtiva = 'S' };

        _repoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Clinica, bool>>>()))
            .ReturnsAsync(new[] { clinica });

        var result = await _sut.LoginAsync(new LoginDto { DsEmail = "vet@clinic.com", DsSenha = "secret" });

        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }
}
