using Discord;
using Discord.WebSocket;
using NHSE.Core;
using SysBot.Base;
using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace SysBot.ACNHOrders
{
    public class OrderRequest<T> : IACNHOrderNotifier<T> where T : Item, new()
    {
        public MultiItem ItemOrderData { get; }
        public ulong UserGuid { get; }
        public ulong OrderID { get; }
        public string VillagerName { get; }
        private SocketUser Trader { get; }
        private ISocketMessageChannel CommandSentChannel { get; }
        public Action<CrossBot>? OnFinish { private get; set; }
        public T[] Order { get; } // stupid but I cba to work on this part anymore
        public VillagerRequest? VillagerOrder { get; }
       

        public OrderRequest(MultiItem data, T[] order, ulong user, ulong orderId, SocketUser trader, ISocketMessageChannel commandSentChannel, VillagerRequest? vil)
        {
            ItemOrderData = data;
            UserGuid = user;
            OrderID = orderId;
            Trader = trader;
            CommandSentChannel = commandSentChannel;
            Order = order;
            VillagerName = trader.Username;
            VillagerOrder = vil;
        }

        private string GetItemSummary()
        {
            if (Order == null || Order.Length == 0)
                return "No items";

            // Filter out empty items and group by name
            var validItems = Order.Where(item => item.ItemId != Item.NONE).ToArray();
            if (validItems.Length == 0)
                return "No items";

            var itemCounts = new Dictionary<string, int>();
            foreach (var item in validItems)
            {
                var itemName = GameInfo.Strings.GetItemName(item.ItemId);
                if (!itemCounts.ContainsKey(itemName))
                    itemCounts[itemName] = 0;
                itemCounts[itemName]++;
            }

            var sb = new StringBuilder();
            foreach (var kvp in itemCounts)
            {
                if (kvp.Value > 1)
                    sb.AppendLine($"• {kvp.Key} x{kvp.Value}");
                else
                    sb.AppendLine($"• {kvp.Key}");
            }

            return sb.ToString();
        }

        public void OrderCancelled(CrossBot routine, string msg, bool faulted)
        {
            OnFinish?.Invoke(routine);

            var embed = new EmbedBuilder()
                .WithTitle("Order Cancelled")
                .WithColor(Color.Red)
                .WithDescription(msg)
                .AddField("Items Ordered", GetItemSummary(), inline: false)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            // Send DM to the trader
            Trader.SendMessageAsync(embed: embed);

            // Also notify the command channel when the cancellation was user-visible (not a fault)
            if (!faulted)
                CommandSentChannel.SendMessageAsync(text: $"{Trader.Mention} - Your order has been cancelled:", embed: embed);
        }

        public void OrderInitializing(CrossBot routine, string msg)
        {
            var embed = new EmbedBuilder()
                .WithTitle("Order Initializing")
                .WithColor(Color.Gold)
                .WithDescription("Please ensure your inventory is **empty**, then talk to Orville and stay on the Dodo code entry screen. I will send your Dodo code shortly.")
                .AddField("Items Ordered", GetItemSummary(), inline: false)
                .WithFooter(footer => footer.WithText("ACNH Orders"))
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            try
            {
                Trader.SendMessageAsync(embed: embed);
            }
            catch (Exception e)
            {
                LogUtil.LogError("Failed sending order initializing embed: " + e.Message + "\n" + e.StackTrace, "Discord");
            }
        }

        public void OrderReady(CrossBot routine, string msg, string dodo)
        {
            try
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Order Ready")
                    .WithColor(Color.Green)
                    .WithDescription($"{Trader.Mention} I'm waiting for you! {(!string.IsNullOrWhiteSpace(msg) ? msg : string.Empty)}")
                    .AddField("Dodo Code", $" {dodo}", true)
                    .AddField("Items Ordered", GetItemSummary(), inline: false)
                    .WithFooter(footer => footer.WithText("ACNH Orders"))
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .Build();

                Trader.SendMessageAsync(embed: embed);
            }
            catch (Exception e)
            {
                LogUtil.LogError("Failed sending dodo code: " + e.Message + "\n" + e.StackTrace, "Discord");
            }
        }

        public void OrderFinished(CrossBot routine, string msg)
        {
            OnFinish?.Invoke(routine);

            var embed = new EmbedBuilder()
                .WithTitle("Order Complete")
                .WithColor(Color.Green)
                .WithDescription(string.IsNullOrWhiteSpace(msg) ? "Your order is complete. Thanks for your order!" : $"Your order is complete. {msg}")
                .AddField("Items Delivered", GetItemSummary(), inline: false)
                .WithFooter(footer => footer.WithText("ACNH Orders"))
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            try
            {
                Trader.SendMessageAsync(embed: embed);
            }
            catch (Exception e)
            {
                LogUtil.LogError("Failed sending order finished embed: " + e.Message + "\n" + e.StackTrace, "Discord");
            }
        }

        public void SendNotification(CrossBot routine, string msg)
        {
            Trader.SendMessageAsync(msg);
        }
    }
}
