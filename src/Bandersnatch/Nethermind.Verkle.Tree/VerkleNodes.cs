using System.Diagnostics;
using Nethermind.Field;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Utils;

namespace Nethermind.Verkle.Tree;
using Fr = FixedFiniteField<BandersnatchScalarFieldStruct>;

public enum NodeType : byte
{
    BranchNode,
    StemNode
}

public readonly struct SuffixTree
{
    private byte[] Stem { get; }
    private Commitment C1 { get; }
    private Commitment C2 { get; }
    public Commitment ExtensionCommitment { get; }
    public Fr InitCommitmentHash { get; }

    public SuffixTree(byte[] stem)
    {
        Stem = stem;
        C1 = new Commitment();
        C2 = new Commitment();
        ExtensionCommitment = new Commitment();
        InitCommitmentHash = Fr.Zero;
        Banderwagon stemCommitment = GetInitialCommitment();
        ExtensionCommitment.AddPoint(stemCommitment);
        InitCommitmentHash = ExtensionCommitment.PointAsField.Dup();
    }

    private Banderwagon GetInitialCommitment() => Committer.ScalarMul(Fr.One, 0) +
                                                  Committer.ScalarMul(Fr.FromBytesReduced(Stem.Reverse().ToArray()), 1);

    public Fr UpdateCommitment(LeafUpdateDelta deltaLeafCommitment)
    {
        Fr prevCommit = ExtensionCommitment.PointAsField.Dup();

        Fr oldC1Value = C1.PointAsField.Dup();
        Fr oldC2Value = C2.PointAsField.Dup();
        if (deltaLeafCommitment.DeltaC1 is not null) C1.AddPoint(deltaLeafCommitment.DeltaC1);
        if (deltaLeafCommitment.DeltaC2 is not null) C2.AddPoint(deltaLeafCommitment.DeltaC2);

        Fr deltaC1Commit = C1.PointAsField - oldC1Value;
        Fr deltaC2Commit = C2.PointAsField - oldC2Value;

        Banderwagon deltaCommit = Committer.ScalarMul(deltaC1Commit, 2)
                                  + Committer.ScalarMul(deltaC2Commit, 3);

        ExtensionCommitment.AddPoint(deltaCommit);
        return ExtensionCommitment.PointAsField - prevCommit;
    }
}

public class StemNode : InternalNode
{
    public StemNode(byte[] stem, Commitment suffixCommitment) : base(NodeType.StemNode, stem, suffixCommitment)
    {
    }
}

public class BranchNode : InternalNode
{
    public BranchNode() : base(NodeType.BranchNode)
    {
    }
}

public class InternalNode
{
    public bool IsStem => _nodeType == NodeType.StemNode;
    public bool IsBranchNode => _nodeType == NodeType.BranchNode;

    public readonly Commitment _internalCommitment;

    private readonly NodeType _nodeType;

    private byte[]? _stem;
    public byte[] Stem
    {
        get
        {
            Debug.Assert(_stem != null, nameof(_stem) + " != null");
            return _stem;
        }
    }

    protected InternalNode(NodeType nodeType, byte[] stem, Commitment suffixCommitment)
    {
        switch (nodeType)
        {
            case NodeType.StemNode:
                _nodeType = NodeType.StemNode;
                _stem = stem;
                _internalCommitment = suffixCommitment;
                break;
            case NodeType.BranchNode:
            default:
                throw new ArgumentOutOfRangeException(nameof(nodeType), nodeType, null);
        }
    }

    protected InternalNode(NodeType nodeType)
    {
        switch (nodeType)
        {
            case NodeType.BranchNode:
                break;
            case NodeType.StemNode:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(nodeType), nodeType, null);
        }
        _nodeType = nodeType;
        _internalCommitment = new Commitment();
    }
    public Fr UpdateCommitment(Banderwagon point)
    {
        Fr prevCommit = _internalCommitment.PointAsField.Dup();
        _internalCommitment.AddPoint(point);
        return _internalCommitment.PointAsField - prevCommit;
    }
}