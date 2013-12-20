namespace VsChromiumPackage.Features.FormatComment {
  public class CommentType {
    private readonly string _token;

    public CommentType(string token) {
      _token = token;
    }

    public string Token { get { return _token; } }
    public string TextPrefix { get { return Token + " "; } }
  }
}