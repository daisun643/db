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

        // 余额变更与充值流水（无商品关联的 Completed 订单）在存储过程内原子完成
        var userId = CurrentUserId();
        var code = OracleProcedure.OutInt32("p_code");
        var message = OracleProcedure.OutText("p_message");
        await OracleProcedure.CallAsync(_db, "sp_wallet_deposit",
            OracleProcedure.InInt32("p_user_id", userId),
            OracleProcedure.InDecimal("p_amount", request.Amount),
            code,
            message,
            OracleProcedure.OutDecimal("p_balance"),
            OracleProcedure.OutInt32("p_transaction_id"));

        if (OracleProcedure.ReadInt32(code) != OracleProcedure.Success)
            return BadRequest(new { message = OracleProcedure.ReadText(message) ?? "充值失败" });

        var wallet = await _db.Wallets.AsNoTracking().FirstAsync(w => w.UserID == userId);
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
