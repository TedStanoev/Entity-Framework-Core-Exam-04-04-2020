namespace TeisterMask.DataProcessor
{
    using System;
    using System.Collections.Generic;

    using System.ComponentModel.DataAnnotations;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Data;
    using Newtonsoft.Json;
    using TeisterMask.Data.Models;
    using TeisterMask.Data.Models.Enums;
    using TeisterMask.DataProcessor.ImportDto;
    using TeisterMask.DataProcessor.XmlConverter;
    using ValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;

    public class Deserializer
    {
        private const string ErrorMessage = "Invalid data!";

        private const string SuccessfullyImportedProject
            = "Successfully imported project - {0} with {1} tasks.";

        private const string SuccessfullyImportedEmployee
            = "Successfully imported employee - {0} with {1} tasks.";

        public static string ImportProjects(TeisterMaskContext context, string xmlString)
        {
            var sb = new StringBuilder();
            var projectsDto = XmlConvert.Deserialize<XmlProjectImportModel>(xmlString, "Projects");

            foreach (var projectDto in projectsDto)
            {
                if (IsValid(projectDto) == false
                    || DateTime.TryParseExact(projectDto.OpenDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime projectOpenDate) == false)
                {
                    sb.AppendLine(ErrorMessage);
                    continue;
                }

                bool hasDueDate = DateTime.TryParseExact(projectDto.DueDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime projectDueDate);

                var project = new Project
                {
                    Name = projectDto.Name,
                    DueDate = hasDueDate ? (DateTime?)projectDueDate : null,
                    OpenDate = projectOpenDate,
                };

                foreach (var taskDto in projectDto.Tasks)
                {
                    if (IsValid(taskDto) == false
                        || DateTime.TryParseExact(taskDto.OpenDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime taskOpenDate) == false
                        || DateTime.TryParseExact(taskDto.DueDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime taskDueDate) == false
                        || taskOpenDate.Date < projectOpenDate.Date)
                    {
                        sb.AppendLine(ErrorMessage);
                        continue;
                    }

                    if (hasDueDate)
                    {
                        if (taskDueDate.Date > projectDueDate.Date)
                        {
                            sb.AppendLine(ErrorMessage);
                            continue;
                        }
                    }

                    project.Tasks.Add(new Task
                    {
                        Name = taskDto.Name,
                        DueDate = taskDueDate,
                        LabelType = (LabelType)taskDto.LabelType,
                        OpenDate = taskOpenDate,
                        ExecutionType = (ExecutionType)taskDto.ExecutionType,
                    });
                }

                context.Projects.Add(project);
                sb.AppendLine(string.Format(SuccessfullyImportedProject, project.Name, project.Tasks.Count));
            }

            context.SaveChanges();
            return sb.ToString().TrimEnd();
        }

        public static string ImportEmployees(TeisterMaskContext context, string jsonString)
        {
            var sb = new StringBuilder();
            var employeesDto = JsonConvert.DeserializeObject<IEnumerable<JsonEmployeeImportModel>>(jsonString);
            var tasks = context.Tasks.ToList();

            foreach (var employeeDto in employeesDto)
            {
                if (IsValid(employeeDto) == false)
                {
                    sb.AppendLine(ErrorMessage);
                    continue;
                }

                var employee = new Employee
                {
                    Username = employeeDto.Username,
                    Email = employeeDto.Email,
                    Phone = employeeDto.Phone
                };

                foreach (var taskId in employeeDto.Tasks.Distinct())
                {
                    if (tasks.Any(t => t.Id == taskId) == false)
                    {
                        sb.AppendLine(ErrorMessage);
                        continue;
                    }

                    employee.EmployeesTasks.Add(new EmployeeTask
                    {
                        Task = tasks.FirstOrDefault(tasks => tasks.Id == taskId)
                    });
                }

                context.Employees.Add(employee);
                sb.AppendLine(string.Format(SuccessfullyImportedEmployee, employee.Username, employee.EmployeesTasks.Count));
            }

            context.SaveChanges();
            return sb.ToString().TrimEnd();
        }

        private static bool IsValid(object dto)
        {
            var validationContext = new ValidationContext(dto);
            var validationResult = new List<ValidationResult>();

            return Validator.TryValidateObject(dto, validationContext, validationResult, true);
        }
    }
}