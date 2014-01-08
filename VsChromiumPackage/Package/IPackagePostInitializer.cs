namespace VsChromiumPackage.Package {
  interface IPackagePostInitializer  {
    int Priority { get; }
    void Run(IVisualStudioPackage package);
  }
}