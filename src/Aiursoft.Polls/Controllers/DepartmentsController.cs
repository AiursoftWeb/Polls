using Aiursoft.Polls.Authorization;
using Aiursoft.Polls.Entities;
using Aiursoft.Polls.Models.DepartmentsViewModels;
using Aiursoft.Polls.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Polls.Controllers;

[Authorize]
[LimitPerMin]
public class DepartmentsController(TemplateDbContext context) : Controller
{
    [Authorize(Policy = AppPermissionNames.CanManageDepartments)]
    [RenderInNavBar(
        NavGroupName = "Administration",
        NavGroupOrder = 9999,
        CascadedLinksGroupName = "Directory",
        CascadedLinksIcon = "users",
        CascadedLinksOrder = 9998,
        LinkText = "Departments",
        LinkOrder = 3)]
    public async Task<IActionResult> Index()
    {
        var departments = await context.Departments.ToListAsync();
        return this.StackView(new IndexViewModel
        {
            Departments = departments
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManageDepartments)]
    public IActionResult Create()
    {
        return this.StackView(new CreateViewModel());
    }

    [Authorize(Policy = AppPermissionNames.CanManageDepartments)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var dept = new Department { Name = model.Name! };
            context.Departments.Add(dept);
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanManageDepartments)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var dept = await context.Departments.FindAsync(id);
        if (dept == null) return NotFound();

        return this.StackView(new EditViewModel
        {
            Id = dept.Id,
            Name = dept.Name
        });
    }

    [Authorize(Policy = AppPermissionNames.CanManageDepartments)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        if (ModelState.IsValid)
        {
            var dept = await context.Departments.FindAsync(model.Id);
            if (dept == null) return NotFound();

            dept.Name = model.Name!;
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return this.StackView(model);
    }

    [Authorize(Policy = AppPermissionNames.CanManageDepartments)]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var dept = await context.Departments.FindAsync(id);
        if (dept == null) return NotFound();

        return this.StackView(new DeleteViewModel { Department = dept });
    }

    [Authorize(Policy = AppPermissionNames.CanManageDepartments)]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var dept = await context.Departments.FindAsync(id);
        if (dept != null)
        {
            context.Departments.Remove(dept);
            await context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
