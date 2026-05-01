namespace Kura.Application.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Kura.Application.DTOs.Auth;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

public sealed class AuthService : IAuthService
{
    private readonly IRepository<Clinica> _clinicaRepository;
    private readonly IConfiguration _configuration;

    public AuthService(IRepository<Clinica> clinicaRepository, IConfiguration configuration)
    {
        _clinicaRepository = clinicaRepository;
        _configuration = configuration;
    }

    public async Task<TokenResponseDto> LoginAsync(LoginDto dto)
    {
        var clinicas = await _clinicaRepository.FindAsync(c => c.DsEmail == dto.DsEmail);
        var clinica = clinicas.FirstOrDefault()
            ?? throw new RegraDeNegocioException("Email ou senha inválidos.");

        if (!BCrypt.Net.BCrypt.Verify(dto.DsSenha, clinica.DsSenha))
            throw new RegraDeNegocioException("Email ou senha inválidos.");

        var expiresAt = DateTime.UtcNow.AddHours(
            _configuration.GetValue<int>("Jwt:ExpiryHours", 8));

        var token = GenerateToken(clinica, expiresAt);

        return new TokenResponseDto
        {
            AccessToken = token,
            ExpiresAt = expiresAt
        };
    }

    private string GenerateToken(Clinica clinica, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("clinicaId", clinica.Id.ToString()),
            new Claim("veterinarioId", string.Empty),
            new Claim(JwtRegisteredClaimNames.Email, clinica.DsEmail)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
