namespace AlbionDataHandlers.Entities
{
    public class InterpolatableEntity
    {
        public int Id { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }

        // Hata veren kýsýmlar bunlardý, "set" özellikleri public yapýldý.
        public float CurrentLerpedX { get; set; }
        public float CurrentLerpedY { get; set; }

        public InterpolatableEntity() { }

        public InterpolatableEntity(int id, float x, float y)
        {
            Id = id;
            PositionX = x;
            PositionY = y;
            CurrentLerpedX = x;
            CurrentLerpedY = y;
        }
    }
}


