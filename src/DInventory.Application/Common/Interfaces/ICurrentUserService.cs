using DInventory.Application.Common.Models;

namespace DInventory.Application.Common.Interfaces;

public interface ICurrentUserService
{
    CurrentUser GetCurrentUser();
}
