using Backend.Data;
using Backend.Models;
using Backend.Models.DTOs;
using Backend.Authorization;
using Backend.Services;
using Backend.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Controllers;

[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly AppDbContext _db;

    public WalletController(AppDbContext db) => _db = db;

    [HttpGet("me")]
    public async Task<ActionResult<WalletResponse>> GetMine()
    {
        var wallet = await GetOrCreateWalletAsync(CurrentUserId());
        await _db.SaveChangesAsync();
        return Ok(MapWallet(wallet));
    }

    [HttpPost("deposit")]
    public async Task<ActionResult<WalletResponse>> Deposit([FromBody] DepositWalletRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var wallet = await GetOrCreateWalletAsync(CurrentUserId());
        wallet.Balance = (wallet.Balance ?? 0) + request.Amount;
        await _db.SaveChangesAsync();

        return Ok(MapWallet(wallet));
    }

    private int CurrentUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    private async Task<Wallet> GetOrCreateWalletAsync(int userId)
    {
        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserID == userId);
        if (wallet != null)
            return wallet;

        wallet = new Wallet
        {
            UserID = userId,
            Balance = 0
        };
        _db.Wallets.Add(wallet);
        return wallet;
    }

    private static WalletResponse MapWallet(Wallet wallet)
    {
        return new WalletResponse
        {
            WalletID = wallet.WalletID,
            Balance = wallet.Balance ?? 0,
            UserID = wallet.UserID
        };
    }
}
