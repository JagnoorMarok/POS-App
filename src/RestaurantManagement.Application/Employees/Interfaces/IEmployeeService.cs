using RestaurantManagement.Application.Employees.DTOs;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Employees.Interfaces;

public interface IEmployeeService
{
    Task<IReadOnlyList<EmployeeDto>> GetEmployeesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<EmployeeDto?> GetEmployeeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeDto?> GetEmployeeByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default);
    Task<EmployeeDto> UpdateEmployeeAsync(UpdateEmployeeRequest request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task AdminResetPasswordAsync(AdminResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task ChangeRoleAsync(Guid employeeId, EmployeeRole newRole, CancellationToken cancellationToken = default);
    Task DeactivateEmployeeAsync(Guid id, CancellationToken cancellationToken = default);
    Task ActivateEmployeeAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteEmployeeAsync(Guid id, CancellationToken cancellationToken = default);
}
