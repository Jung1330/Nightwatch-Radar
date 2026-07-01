using System;

namespace AOSnifferNET
{
    // {"0":8,"1":"accessories","2":"bag","3":"5","4":0,"5":"8","6":'query text', "7":0,"8":"3","9":50,"11":0,"12":true,"253":76} (id, category, subcategory, quality, unknown, tier, unknown, enhancement...
    internal class opAuctionGetAny
    {
        public int id;
        public Int16[] itemsID;
        public string category;
        public string subcategory;
        public int quality;
        public int tier;
        public int enchantment;

        public opAuctionGetAny(int id, Int16[] itemsID, string category, string subcategory, int quality, int tier, int enchantment)
        {
            this.id = id;
            this.itemsID = itemsID;
            this.category = category;
            this.subcategory = subcategory;
            this.quality = quality;
            this.tier = tier;
            this.enchantment = enchantment;
        }
    }
}


