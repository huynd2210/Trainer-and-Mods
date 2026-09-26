using System;
using System.Numerics;
using NuclearOptionAMRAAM;

static class Program
{
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static void Main()
    {
        Check(LoftProfile.Rise(5000,20000,18000)==0,"Close launches must stay direct");
        Check(LoftProfile.Rise(20000,20000,18000)==0,"Threshold must stay direct");
        Check(LoftProfile.Rise(20001,20000,18000)<1,"Crossing threshold must be smooth");
        Check(LoftProfile.Rise(140000,20000,18000)==18000,"Rise cap");
        Check(LoftProfile.Advance(.6f,80000,40000)==.6f,"Receding target must not rewind arc");
        foreach(float start in new[]{100f,1000f,10000f})
        foreach(float end in new[]{100f,1000f,12000f})
        {
            Check(LoftProfile.Height(0,start,end,18000)==start,"Start endpoint");
            Check(LoftProfile.Height(1,start,end,18000)==end,"End endpoint");
            Check(LoftProfile.Height(.5f,start,end,18000)>(start+end)/2+17999,"Arc apex");
        }
        // Follow the same look-ahead arc against stationary targets with a limited
        // heading response. This verifies guidance shape, not the game's aerodynamics.
        foreach(float distance in new[]{5000f,20000f,25000f,60000f,140000f})
        {
            var pos=new Vector2(0,1000); float heading=0, speed=250, progress=0, maxY=pos.Y;
            float rise=LoftProfile.Rise(distance,20000,18000); bool terminal=rise==0, descended=false;
            float closest=float.MaxValue, time=0;
            for(int i=0;i<14000;i++)
            {
                float remaining=distance-pos.X; var delta=new Vector2(remaining,1000-pos.Y);
                closest=Math.Min(closest,delta.Length()); if(closest<70) break;
                if(delta.Length()<=12000 || remaining<1500 || progress>=.97f) terminal=true;
                Vector2 aim=new Vector2(distance,1000);
                if(!terminal)
                {
                    progress=LoftProfile.Advance(progress,distance,remaining);
                    float ahead=Math.Min(remaining,Math.Clamp(speed*4,1800,6000));
                    float t=Math.Min(1,progress+ahead/distance);
                    aim=new Vector2(pos.X+ahead,LoftProfile.Height(t,1000,1000,rise));
                }
                Vector2 dir=aim-pos;
                float desired=MathF.Atan2(dir.Y,dir.X), error=MathF.Atan2(MathF.Sin(desired-heading),MathF.Cos(desired-heading));
                heading+=Math.Clamp(error,-.006f,.006f);
                speed=Math.Min(1180,speed+40*.02f); pos+=new Vector2(MathF.Cos(heading),MathF.Sin(heading))*speed*.02f;
                time+=.02f; maxY=Math.Max(maxY,pos.Y); if(heading<-.01) descended=true;
            }
            Check(closest<70,$"No intercept for {distance}: miss {closest}");
            Check(time<155,$"Motor endurance insufficient: {distance}, {time}");
            if(distance<=20000) Check(maxY<1001,"Close launch climbed");
            if(distance>=60000) Check(maxY>5000 && descended,"Long launch must climb and dive");
            Console.WriteLine($"{distance/1000:F0} km: max altitude {maxY:F0} m, elapsed {time:F1} s, closest approach {closest:F1} m; {(rise>0?"LOFT":"DIRECT")}");
        }
        Console.WriteLine($"PASS: {checks} assertions. Kinematic guidance check only; live game aerodynamics require gameplay testing.");
    }
}
