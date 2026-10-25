namespace EdsDcfNet.Models;

using System.Xml.Linq;

/// <summary>A comment or processing instruction and its position among the child elements.</summary>
internal sealed class XddPreservedNode
{
    internal XddPreservedNode(XNode node, string? followingElementName, int followingElementOccurrence)
    {
        Node = node;
        FollowingElementName = followingElementName;
        FollowingElementOccurrence = followingElementOccurrence;
    }

    /// <summary>The preserved node; never an element or formatting-only text.</summary>
    internal XNode Node { get; }

    /// <summary>Local name of the element before which the node appeared, or null for a trailing node.</summary>
    internal string? FollowingElementName { get; }

    /// <summary>Zero-based occurrence of the following element among siblings with the same local name.</summary>
    internal int FollowingElementOccurrence { get; }

    /// <summary>Schema type of a following generated profile, stable across profile reordering.</summary>
    internal string? FollowingProfileBodyType { get; set; }

    internal XddPreservedNode Clone()
        => new(Node is XComment comment
            ? new XComment(comment.Value)
            : new XProcessingInstruction(((XProcessingInstruction)Node).Target, ((XProcessingInstruction)Node).Data),
            FollowingElementName,
            FollowingElementOccurrence)
        {
            FollowingProfileBodyType = FollowingProfileBodyType,
        };
}
