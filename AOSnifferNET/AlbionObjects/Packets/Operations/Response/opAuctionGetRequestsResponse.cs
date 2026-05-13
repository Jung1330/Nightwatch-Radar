using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AOSnifferNET
{
    //{"0":["{\"Id\":8755020507,\"UnitPriceSilver\":19999980000,\"TotalPriceSilver\":19999980000,\"Amount\":1,\"Tier\":8,\"IsFinished\":false,\"AuctionType\":\"offer\",\"HasBuyerFetched\":false,\"HasSellerFetched\":false,\"SellerCharacterId\":\"cc61fb20-fa4a-4fe2-843e-1f71f25d0f4a\",\"SellerName\":\"SoloAGG\",\"BuyerCharacterId\":null,\"BuyerName\":null,\"ItemTypeId\":\"T8_FURNITUREITEM_TROPHY_ORE\",\"ItemGroupTypeId\":\"T8_FURNITUREITEM_TROPHY_ORE\",\"EnchantmentLevel\":0,\"QualityLevel\":1,\"Expires\":\"2022-06-17T01:47:22.125773\",\"ReferenceId\":\"8f62ce8b-3521-48dc-a429-32671c5aaeeb\"}","{\"Id\":8715166777,\"UnitPriceSilver\":20000000000,\"TotalPriceSilver\":20000000000,\"Amount\":1,\"Tier\":8,\"IsFinished\":false,\"AuctionType\":\"offer\",\"HasBuyerFetched\":false,\"HasSellerFetched\":false,\"SellerCharacterId\":\"d35fd339-c5a7-45e8-8ed7-766cfc5cf33e\",\"SellerName\":\"CAHEK48\",\"BuyerCharacterId\":null,\"BuyerName\":null,\"ItemTypeId\":\"T8_FURNITUREITEM_TROPHY_ORE\",\"ItemGroupTypeId\":\"T8_FURNITUREITEM_TROPHY_ORE\",\"EnchantmentLevel\":0,\"QualityLevel\":1,\"Expires\":\"2022-06-08T22:04:09.684997\",\"ReferenceId\":\"2d4a743f-0e0d-46a1-bf62-4c195d1b42cf\"}","{\"Id\":8745473936,\"UnitPriceSilver\":20000000000,\"TotalPriceSilver\":60000000000,\"Amount\":3,\"Tier\":8,\"IsFinished\":false,\"AuctionType\":\"offer\",\"HasBuyerFetched\":false,\"HasSellerFetched\":false,\"SellerCharacterId\":\"13314bf9-3080-459e-ade7-bae3a0f794ac\",\"SellerName\":\"AnemiaxD\",\"BuyerCharacterId\":null,\"BuyerName\":null,\"ItemTypeId\":\"T8_FURNITUREITEM_TROPHY_ORE\",\"ItemGroupTypeId\":\"T8_FURNITUREITEM_TROPHY_ORE\",\"EnchantmentLevel\":0,\"QualityLevel\":1,\"Expires\":\"2022-06-15T02:11:45.049899\",\"ReferenceId\":\"14b2dda0-d63b-4c28-a35d-c227b6d2b4be\"}"],"253":76}
    internal class opAuctionGetRequestsResponse
    {
        public List<AuctionItems> requestsList;

        public opAuctionGetRequestsResponse(List<AuctionItems> requestsList)
        {
            this.requestsList = requestsList;
        }
    }
}


