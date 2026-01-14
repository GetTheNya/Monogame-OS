using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.Scenes;

public abstract class Scene {
    public SceneManager SceneManager { get; set; }

    public virtual void LoadContent(ContentManager content) { }
    public virtual void UnloadContent() { }
    public virtual void Update(GameTime gameTime) { }
    public virtual void Draw(SpriteBatch spriteBatch, Graphics.ShapeBatch shapeBatch) { }
}
