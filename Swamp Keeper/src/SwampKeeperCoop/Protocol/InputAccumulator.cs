namespace SwampKeeperCoop.Protocol;

// TCP can deliver a complete press/release between host simulation frames.
// Accumulate edges separately from the latest held state so short taps survive batching.
public sealed class InputAccumulator
{
    public InputState Current {get;private set;}=new();
    public long Sequence {get;private set;}=-1;
    public uint Pressed {get;private set;}
    public uint Released {get;private set;}
    public void BeginFrame(){Pressed=0;Released=0;}
    public bool Accept(InputState value)
    {
        value=value.Validated();if(value.Sequence<=Sequence)return false;
        Pressed|=value.Pressed|(value.Keys&~Current.Keys);
        Released|=value.Released|(Current.Keys&~value.Keys);
        Sequence=value.Sequence;Current=value;return true;
    }
    public void Clear(){Pressed=0;Released|=Current.Keys;Current=new InputState();}
    public void Reset(){Current=new InputState();Sequence=-1;Pressed=0;Released=0;}
}
