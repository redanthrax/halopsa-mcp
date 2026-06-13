namespace HaloPsaMcp.Modules.Authentication.Services;

public interface IExpiring {
    long Expires { get; }
}
