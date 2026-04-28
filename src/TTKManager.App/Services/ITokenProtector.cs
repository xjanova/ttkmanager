namespace TTKManager.App.Services;

public interface ITokenProtector
{
    byte[] Protect(string plaintext);
    string Unprotect(byte[] ciphertext);
}
