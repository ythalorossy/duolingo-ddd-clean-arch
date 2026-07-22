namespace Learning.Domain;

// A lesson node's progression state on the learner's course map.
// Serialized to the wire as its string name (order here is irrelevant to the contract).
public enum NodeStatus
{
    Locked,
    Unlocked,
    Completed
}
