using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Models;
using Prophet.Client.Trading.Exchanges;

namespace Prophet.Client.Trading.OrderManagement;

/// <summary>
/// 实盘订单同步器
/// 负责与交易所同步订单状态，确保本地订单与交易所一致
/// </summary>
public class LiveOrderSynchronizer
{
    private readonly IExchange _exchange;
    private Timer? _syncTimer;
    private bool _isSyncing;
    
    public event EventHandler<OrderSyncEventArgs>? OrderSynced;
    
    public LiveOrderSynchronizer(IExchange exchange)
    {
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
    }
    
    /// <summary>
    /// 启动自动同步
    /// </summary>
    public void StartAutoSync(int intervalSeconds = 30)
    {
        StopAutoSync();
        
        _syncTimer = new Timer(
            async _ => await SyncTimerCallback(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(intervalSeconds));
        
        Console.WriteLine($"✅ [LiveOrderSynchronizer] 启动自动同步，间隔: {intervalSeconds}秒");
    }
    
    /// <summary>
    /// 停止自动同步
    /// </summary>
    public void StopAutoSync()
    {
        _syncTimer?.Dispose();
        _syncTimer = null;
        Console.WriteLine($"⏹️ [LiveOrderSynchronizer] 停止自动同步");
    }
    
    /// <summary>
    /// 定时器回调
    /// </summary>
    private async Task SyncTimerCallback()
    {
        if (_isSyncing)
        {
            return; // 避免重复同步
        }
        
        _isSyncing = true;
        try
        {
            // 这里可以实现定时同步逻辑
            // 例如：检查订单状态变化、同步账户余额等
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [LiveOrderSynchronizer] 自动同步失败: {ex.Message}");
        }
        finally
        {
            _isSyncing = false;
        }
    }
    
    /// <summary>
    /// 同步未完成订单
    /// </summary>
    public async Task SynchronizeOpenOrdersAsync(List<Order> localOrders, string symbol)
    {
        try
        {
            Console.WriteLine($"🔄 [LiveOrderSynchronizer] 同步未完成订单...");
            
            // 从交易所获取未完成订单
            var exchangeOrders = await _exchange.GetOpenOrdersAsync(symbol);
            
            // 检查本地订单是否在交易所存在
            foreach (var localOrder in localOrders.ToList())
            {
                var exchangeOrder = exchangeOrders.FirstOrDefault(o => o.OrderId == localOrder.Id);
                
                if (exchangeOrder == null)
                {
                    // 本地有但交易所没有，可能已成交或被取消
                    Console.WriteLine($"⚠️ [LiveOrderSynchronizer] 订单 {localOrder.Id} 在交易所不存在");
                    
                    // 查询订单详情
                    try
                    {
                        var orderInfo = await _exchange.GetOrderAsync(localOrder.Id, symbol);
                        if (orderInfo.Status == "FILLED" || orderInfo.Status == "CANCELED")
                        {
                            Console.WriteLine($"   订单已{orderInfo.Status}");
                            // 更新本地订单状态
                            UpdateLocalOrderFromExchange(localOrder, orderInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   查询订单失败: {ex.Message}");
                    }
                }
                else
                {
                    // 更新订单状态
                    UpdateLocalOrderFromExchange(localOrder, exchangeOrder);
                }
            }
            
            // 检查交易所是否有本地没有的订单
            foreach (var exchangeOrder in exchangeOrders)
            {
                var localOrder = localOrders.FirstOrDefault(o => o.Id == exchangeOrder.OrderId);
                if (localOrder == null)
                {
                    Console.WriteLine($"⚠️ [LiveOrderSynchronizer] 发现未知订单: {exchangeOrder.OrderId}");
                    // 可以选择添加到本地列表或忽略
                }
            }
            
            Console.WriteLine($"✅ [LiveOrderSynchronizer] 订单同步完成");
            
            OrderSynced?.Invoke(this, new OrderSyncEventArgs 
            { 
                SyncTime = DateTime.UtcNow,
                LocalOrderCount = localOrders.Count,
                ExchangeOrderCount = exchangeOrders.Count
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [LiveOrderSynchronizer] 订单同步失败: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 从交易所订单信息更新本地订单
    /// </summary>
    private void UpdateLocalOrderFromExchange(Order localOrder, Trading.Models.OrderInfo exchangeOrder)
    {
        // 更新订单状态
        if (exchangeOrder.Status == "FILLED")
        {
            localOrder.Status = OrderStatus.FILLED;
            localOrder.FilledPrice = exchangeOrder.AvgPrice;
            
            if (localOrder.OpenPrice == 0)
            {
                localOrder.OpenPrice = exchangeOrder.AvgPrice;
            }
        }
        else if (exchangeOrder.Status == "CANCELED")
        {
            localOrder.Status = OrderStatus.CANCELLED;
        }
        else if (exchangeOrder.Status == "PARTIALLY_FILLED")
        {
            localOrder.Status = OrderStatus.PENDING;
            localOrder.FilledPrice = exchangeOrder.AvgPrice;
        }
        
        // 更新已成交数量
        if (exchangeOrder.FilledQuantity > 0)
        {
            localOrder.Quantity = exchangeOrder.FilledQuantity;
        }
    }
}

/// <summary>
/// 订单同步事件参数
/// </summary>
public class OrderSyncEventArgs : EventArgs
{
    public DateTime SyncTime { get; set; }
    public int LocalOrderCount { get; set; }
    public int ExchangeOrderCount { get; set; }
}

