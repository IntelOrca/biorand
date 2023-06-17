namespace IntelOrca.Biohazard
{
    public interface IModelMesh
    {
        BioVersion Version { get; }
        int NumParts { get; }
        byte[] GetBytes();
    }
}
