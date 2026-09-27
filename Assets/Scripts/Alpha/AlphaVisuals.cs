using UnityEngine;

public static class AlphaVisuals
{
    private static Sprite square;
    public static Sprite Square => square != null ? square : square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
}
