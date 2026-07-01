namespace AOSnifferNET
{
    // Join: {"0":1668168,"1":"d5BAcx01AkmtmRylW1C1Zg==","2":"LaMiniMina","3":45,"4":22,"5":1,"6":"AAABAg==","7":"AAEBAw==","8":"3005","9":[19.83802,79.20444],"10":263.660828,"11":1485.0,"12":1485.0,"13":14.8485775,"14":637886969857070373,"15":151.0,"16":151.0,"17":1.89044285,"18":637886969857070373,"19":1804.0,"20":1804.0,"21":18.04,"22":637886969857070373,"23":30000.0,"24":30000.0,"25":0.115740739,"26":637886969830226508,"28":110145633500,"30":34913534000,"31":{},"32":1920000,"33":637886941200818730,"34":9999990000,"35":28800000,"36":21777.4531,"37":637867065475195056,"38":"AAAAAAA=","39":0,"40":"","41":"","42":0,"43":true,"44":"a6ZwfLrVvUGZLp8GJ4BPIQ==","45":[0,0,0,0,1668169,1668171,0,1668170,0,0],"47":"WC2Do1bbX0OywMy4kuZXoQ==","48":[1668173,1668174,1668177,1668172,1668175,0,0,0,0,0,1668176,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],"50":0,"52":637863448742364815,"53":[55.55903,-164.03035],"54":"3210","55":"3003","57":[0.0,0.0],"59":"","60":637886969857070373,"61":662517406,"70":"","72":"","74":30000000,"75":0,"77":637960150800818730,"78":123,"79":637853730366526610,"80":637853755364850718,"81":"","82":[],"83":"","85":true,"86":true,"88":637886969816343412,"89":637886630199702987,"90":519,"91":{},"92":"AAAAAAAA","93":0,"95":[-1,-1,-1,-1,-1,-1,-1],"96":0,"253":2}
    // 0: character id
    // 1: mark id
    // 2: character name
    // 8: current cluster
    // 9: current position in the map
    // 10: facing angle
    // 11: current health
    // 12: max health
    // 15: current energy
    // 16: max energy
    // 28: silver
    // 43: faction
    // 46: items equiped
    // 49: skills
    // 60: previous cluster
    internal class OpJoin
    {
        public int characterId;
        public byte[] markId;
        public string characterName;
        public string cluster;
        public float[] pos;
        public float angle;
        public int currentHealth;
        public int maxHealth;
        public int currentEnergy;
        public int maxEnergy;
        public short[] items;
        public short[] skills;
        public int faction;

        public OpJoin(int characterId, byte[] markId, string characterName, string cluster, float[] pos, float angle, int currentHealth, int maxHealth, int currentEnergy, int maxEnergy, int faction)
        {
            this.characterId = characterId;
            this.markId = markId;
            this.characterName = characterName;
            this.cluster = cluster;
            this.pos = pos;
            this.angle = angle;
            this.currentHealth = currentHealth;
            this.maxHealth = maxHealth;
            this.currentEnergy = currentEnergy;
            this.maxEnergy = maxEnergy;
            this.faction = faction;
        }

        public void addItemsAndSkills(short[] items, short[] skills)
        {
            this.items = items;
            this.skills = skills;
        }
    }
}


