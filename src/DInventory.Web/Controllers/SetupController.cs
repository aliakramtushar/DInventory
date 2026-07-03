using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DInventory.Web.Controllers;

/// <summary>Entry hub for every "setup" (master data / configuration) page. Only SuperAdmin and Admin can reach it;
/// individual pages linked from here still enforce their own fine-grained RoleMenuPermissions on top of this.</summary>
[Authorize(Roles = "SuperAdmin,Admin")]
public class SetupController : Controller
{
    public IActionResult Index() => View();
}
