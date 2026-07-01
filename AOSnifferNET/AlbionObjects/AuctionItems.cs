using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    //{\"Id\":8755020507,\"UnitPriceSilver\":19999980000,\"TotalPriceSilver\":19999980000,\"Amount\":1,\"Tier\":8,\"IsFinished\":false,\"AuctionType\":\"offer\",
    //\"HasBuyerFetched\":false,\"HasSellerFetched\":false,\"SellerCharacterId\":\"cc61fb20-fa4a-4fe2-843e-1f71f25d0f4a\",\"SellerName\":\"SoloAGG\",
    //\"BuyerCharacterId\":null,\"BuyerName\":null,\"ItemTypeId\":\"T8_FURNITUREITEM_TROPHY_ORE\",\"ItemGroupTypeId\":\"T8_FURNITUREITEM_TROPHY_ORE\",
    //\"EnchantmentLevel\":0,\"QualityLevel\":1,\"Expires\":\"2022-06-17T01:47:22.125773\",\"ReferenceId\":\"8f62ce8b-3521-48dc-a429-32671c5aaeeb\"}
    internal class AuctionItems
    {
        public long id;
        public long unitPriceSilver;
        public long totalPriceSilver;
        public int amount;
        public short tier;
        public bool isFinished;
        public string auctionType;
        public bool hasBuyerFetched;
        public bool hasSellerFetched;
        public string sellerCharacterId;
        public string sellerName;
        public string buyerCharacterId;
        public string buyerName;
        public string itemTypeId;
        public string itemGroupTypeId;
        public short enchantmentLevel;
        public short qualityLevel;
        public string expires;
        public string referenceId;

        public AuctionItems(long id, long unitPriceSilver, long totalPriceSilver, int amount, short tier, bool isFinished, string auctionType, bool hasBuyerFetched, bool hasSellerFetched, string sellerCharacterId, string sellerName, string buyerCharacterId, string buyerName, string itemTypeId, string itemGroupTypeId, short enchantmentLevel, short qualityLevel, string expires, string referenceId)
        {
            this.id = id;
            this.unitPriceSilver = unitPriceSilver;
            this.totalPriceSilver = totalPriceSilver;
            this.amount = amount;
            this.tier = tier;
            this.isFinished = isFinished;
            this.auctionType = auctionType;
            this.hasBuyerFetched = hasBuyerFetched;
            this.hasSellerFetched = hasSellerFetched;
            this.sellerCharacterId = sellerCharacterId;
            this.sellerName = sellerName;
            this.buyerCharacterId = buyerCharacterId;
            this.buyerName = buyerName;
            this.itemTypeId = itemTypeId;
            this.itemGroupTypeId = itemGroupTypeId;
            this.enchantmentLevel = enchantmentLevel;
            this.qualityLevel = qualityLevel;
            this.expires = expires;
            this.referenceId = referenceId;
        }
    }
}


