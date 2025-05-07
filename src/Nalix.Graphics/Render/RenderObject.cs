using Nalix.Graphics.Scene;
using SFML.Graphics;

namespace Nalix.Graphics.Render;

public abstract class RenderObject : SceneObject
{
    protected abstract Drawable GetDrawable();

    public virtual void Render(RenderTarget target)
    {
        if (Visible) target.Draw(GetDrawable());
    }

    public bool Visible { get; private set; } = true;

    public void Conceal() => Visible = false;

    public void Reveal() => Visible = true;

    private int zIndex;

    public void SetZIndex(int index)
    {
        zIndex = index;
    }

    public static int CompareByZIndex(RenderObject? r1, RenderObject? r2)
    {
        if (r1 == null && r2 == null) return 0;
        if (r1 == null) return -1;
        if (r2 == null) return 1;
        return r1.zIndex - r2.zIndex;
    }
}
