using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Prophet.Client.Data.Symbols;

namespace Prophet.Client.Database.Repositories;

public sealed class InstrumentRepository
{
    private const string TableName = "instruments";

    /// <summary>
    /// 获取已启用的交易标的列表
    /// </summary>
    public async Task<List<InstrumentDefinition>> GetEnabledAsync()
    {
        const string sql = @"
SELECT symbol_key AS SymbolKey,
       base_symbol AS BaseSymbol,
       exchange AS Exchange,
       market_type AS MarketType,
       venue_inst_id AS VenueInstrumentId,
       is_enabled AS IsEnabled
FROM instruments
WHERE is_enabled = 1
ORDER BY base_symbol ASC, exchange ASC, market_type ASC;";

        using var connection = DBHelper.CreateConnection();
        var rows = await connection.QueryAsync<InstrumentDefinition>(sql);
        return rows.ToList();
    }

    public async Task UpsertAsync(InstrumentDefinition instrument)
    {
        if (string.IsNullOrWhiteSpace(instrument.SymbolKey))
        {
            throw new ArgumentException("SymbolKey 不能为空", nameof(instrument));
        }

        const string sql = @"
INSERT INTO instruments(symbol_key, base_symbol, exchange, market_type, venue_inst_id, is_enabled, updated_at)
VALUES(@SymbolKey, @BaseSymbol, @Exchange, @MarketType, @VenueInstrumentId, @IsEnabled, datetime('now', 'localtime'))
ON CONFLICT(symbol_key) DO UPDATE SET
  base_symbol = excluded.base_symbol,
  exchange = excluded.exchange,
  market_type = excluded.market_type,
  venue_inst_id = excluded.venue_inst_id,
  is_enabled = excluded.is_enabled,
  updated_at = datetime('now', 'localtime');";

        using var connection = DBHelper.CreateConnection();
        await connection.ExecuteAsync(sql, instrument);
    }

    public async Task EnsureSeedAsync(IEnumerable<InstrumentDefinition> seeds)
    {
        var list = seeds.Where(s => !string.IsNullOrWhiteSpace(s.SymbolKey)).ToList();
        if (list.Count == 0)
        {
            return;
        }

        const string sql = @"
INSERT OR IGNORE INTO instruments(symbol_key, base_symbol, exchange, market_type, venue_inst_id, is_enabled)
VALUES(@SymbolKey, @BaseSymbol, @Exchange, @MarketType, @VenueInstrumentId, @IsEnabled);";

        using var connection = DBHelper.CreateConnection();
        using var tx = connection.BeginTransaction();
        try
        {
            foreach (var seed in list)
            {
                await connection.ExecuteAsync(sql, seed, tx);
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task EnsureDefaultSeedAsync()
    {
        var seeds = new List<InstrumentDefinition>();

        var baseSymbols = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT" };
        foreach (var baseSymbol in baseSymbols)
        {
            seeds.Add(InstrumentDefinition.CreateBinanceSwap(baseSymbol));
            seeds.Add(InstrumentDefinition.CreateOkxSwap(baseSymbol));
        }

        await EnsureSeedAsync(seeds);
    }

    public async Task<int> GetEnabledCountAsync()
    {
        const string sql = @"SELECT COUNT(1) FROM instruments WHERE is_enabled = 1;";
        using var connection = DBHelper.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql);
    }

    /// <summary>
    /// 确保交易对存在于 instruments 表中（如果不存在则自动注册）
    /// 用于解决外键约束问题：klines.symbol_key REFERENCES instruments.symbol_key
    /// </summary>
    public async Task EnsureInstrumentExistsAsync(string symbolKey)
    {
        if (string.IsNullOrWhiteSpace(symbolKey))
        {
            throw new ArgumentException("symbolKey 不能为空", nameof(symbolKey));
        }

        // 检查是否已存在
        const string checkSql = @"SELECT COUNT(1) FROM instruments WHERE symbol_key = @SymbolKey;";
        using var connection = DBHelper.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int>(checkSql, new { SymbolKey = symbolKey });
        
        if (exists > 0)
        {
            return; // 已存在，无需注册
        }

        // 解析 symbol_key 格式：BTCUSDT-BINANCE-SWAP
        if (!InstrumentKey.TryParse(symbolKey, out var key))
        {
            Console.WriteLine($"⚠️ [InstrumentRepository] 无法解析 symbol_key: {symbolKey}，跳过自动注册");
            return;
        }

        // 自动注册
        var instrument = new InstrumentDefinition
        {
            SymbolKey = key.ToString().ToUpperInvariant(),
            BaseSymbol = key.BaseSymbol.ToUpperInvariant(),
            Exchange = key.Exchange.ToUpperInvariant(),
            MarketType = key.MarketType.ToUpperInvariant(),
            VenueInstrumentId = key.BaseSymbol.ToUpperInvariant(),
            IsEnabled = 1 // SQLite 使用 INTEGER (1=true, 0=false)
        };

        await UpsertAsync(instrument);
        Console.WriteLine($"✅ [InstrumentRepository] 自动注册交易对: {symbolKey}");
    }
}


