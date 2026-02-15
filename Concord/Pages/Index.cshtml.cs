using Concord.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Concord.Pages;

public class IndexModel : PageModel
{
    public string Message { get; private set; }
    public string PublicIp { get; private set; }

    public async Task OnGet()
    {
        Message = "Hello from the server at " + DateTime.Now;
        PublicIp = await PublicIpService.GetPublicIpAsync();
    }
}
