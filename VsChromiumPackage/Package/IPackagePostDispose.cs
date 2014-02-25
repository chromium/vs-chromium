namespace VsChromiumPackage.Package {
  interface IPackagePostDispose {
    int Priority { get; }
    void Run(IVisualStudioPackage package);
  }
}