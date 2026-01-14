using System;
using Microsoft.Xna.Framework;

namespace TheGame;

public class Fps {
    private double frames = 0;
    private double updates = 0;
    private double elapsed = 0;
    private double last = 0;
    private double now = 0;
    public double msgFrequency = 1.0f;
    public string msg = "";
    public double CurrentFps { get; private set; }

    public void Update(GameTime gameTime) {
        now = gameTime.TotalGameTime.TotalSeconds;
        elapsed = (double)(now - last);
        if (elapsed > msgFrequency) {
            CurrentFps = frames / elapsed;
            msg = " Fps: " + CurrentFps.ToString() + "\n Elapsed time: " + elapsed.ToString() + "\n Updates: " +
                  updates.ToString() + "\n Frames: " + frames.ToString();
            Console.WriteLine(msg);
            elapsed = 0;
            frames = 0;
            updates = 0;
            last = now;
        }

        updates++;
    }

    public void UpdateDrawFps() {
        frames++;
    }
}
