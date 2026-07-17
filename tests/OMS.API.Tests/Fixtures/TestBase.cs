namespace OMS.API.Tests.Fixtures;

public abstract class TestBase
{
    [Route("diagnostics")]
    protected sealed class TestController : ControllerBase;

    protected sealed class TestAuditableEntity : AuditableEntity;

    protected sealed class FakeAuthRepository : IAuthRepository
    {
        public Role SalesOperatorRole { get; } = new()
        {
            Id = Guid.NewGuid(),
            Name = SystemRoleNames.SalesOperator
        };

        public List<User> Users { get; } = [];

        public Task AddUserAsync(User user, CancellationToken cancellationToken)
        {
            Users.Add(user);

            return Task.CompletedTask;
        }

        public Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken)
        {
            return Task.FromResult<Role?>(
                string.Equals(roleName, SalesOperatorRole.Name, StringComparison.Ordinal)
                    ? SalesOperatorRole
                    : null);
        }

        public Task<User?> GetUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            var user = Users.SingleOrDefault(
                candidate => string.Equals(candidate.Email, normalizedEmail, StringComparison.Ordinal));

            return Task.FromResult(user);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<bool> UserExistsByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            return Task.FromResult(Users.Any(
                user => string.Equals(user.Email, normalizedEmail, StringComparison.Ordinal)));
        }
    }

    protected sealed class FakeAuthService(
        AuthUserResponse? registerResponse = null,
        AuthResponse? loginResponse = null) : IAuthService
    {
        public Task<AuthUserResponse> RegisterAsync(
            RegisterRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(registerResponse ?? new AuthUserResponse(
                Guid.NewGuid(),
                request.Email,
                request.FullName,
                SystemRoleNames.SalesOperator));
        }

        public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(loginResponse ?? new AuthResponse(
                "access-token",
                DateTime.UtcNow.AddMinutes(30),
                new AuthUserResponse(
                    Guid.NewGuid(),
                    request.Email,
                    "Sales User",
                    SystemRoleNames.SalesOperator)));
        }
    }

    protected sealed class FakeUserRepository : IUserRepository
    {
        public List<Role> Roles { get; } =
        [
            new() { Id = Guid.NewGuid(), Name = SystemRoleNames.Admin },
            new() { Id = Guid.NewGuid(), Name = SystemRoleNames.Supervisor },
            new() { Id = Guid.NewGuid(), Name = SystemRoleNames.SalesOperator }
        ];

        public List<User> Users { get; } = [];

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            Users.Add(user);

            return Task.CompletedTask;
        }

        public User CreateUser(
            string email,
            string roleName,
            string? fullName = null,
            bool isActive = true)
        {
            var role = Roles.Single(candidate => candidate.Name == roleName);

            return new User
            {
                Id = Guid.NewGuid(),
                Email = User.NormalizeEmail(email),
                FullName = fullName ?? $"{roleName} User",
                PasswordHash = new BCryptPasswordHasher().HashPassword("StrongPassword123!"),
                RoleId = role.Id,
                Role = role,
                IsActive = isActive,
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        public Task<bool> EmailExistsAsync(
            string normalizedEmail,
            Guid? excludingUserId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Users.Any(user =>
                user.Email == normalizedEmail &&
                (!excludingUserId.HasValue || user.Id != excludingUserId.Value)));
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Users.SingleOrDefault(user => user.Id == id));
        }

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Users.SingleOrDefault(user => user.Id == id));
        }

        public Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken)
        {
            return Task.FromResult(Roles.SingleOrDefault(role => role.Name == roleName));
        }

        public Task<PaginatedResult<User>> ListAsync(
            UserListRequest request,
            CancellationToken cancellationToken)
        {
            IEnumerable<User> query = Users;

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var normalizedSearch = User.NormalizeEmail(request.Search);
                var trimmedSearch = request.Search.Trim();

                query = query.Where(user =>
                    user.Email.Contains(normalizedSearch, StringComparison.Ordinal) ||
                    user.FullName.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase));
            }

            query = request.SortBy?.Trim().ToLowerInvariant() switch
            {
                "email" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(user => user.Email)
                    : query.OrderBy(user => user.Email),
                "fullname" or "full_name" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(user => user.FullName)
                    : query.OrderBy(user => user.FullName),
                "role" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(user => user.Role!.Name)
                    : query.OrderBy(user => user.Role!.Name),
                "isactive" or "is_active" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(user => user.IsActive)
                    : query.OrderBy(user => user.IsActive),
                _ => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(user => user.CreatedAtUtc)
                    : query.OrderBy(user => user.CreatedAtUtc)
            };

            var users = query.ToArray();
            var page = users
                .Skip(request.Skip)
                .Take(request.PageSize)
                .ToArray();

            return Task.FromResult(new PaginatedResult<User>(
                page,
                new PaginationMetadata(request.Page, request.PageSize, users.Length)));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    protected sealed class FakeCategoryRepository : ICategoryRepository
    {
        public List<Category> Categories { get; } = [];

        public Task AddAsync(Category category, CancellationToken cancellationToken)
        {
            Categories.Add(category);

            return Task.CompletedTask;
        }

        public Category CreateCategory(
            string name,
            string? description = null,
            bool isActive = true)
        {
            return new Category
            {
                Id = Guid.NewGuid(),
                Name = Category.NormalizeName(name),
                Description = description,
                IsActive = isActive,
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Categories.SingleOrDefault(category => category.Id == id));
        }

        public Task<Category?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Categories.SingleOrDefault(category => category.Id == id));
        }

        public Task<PaginatedResult<Category>> ListAsync(
            CategoryListRequest request,
            CancellationToken cancellationToken)
        {
            IEnumerable<Category> query = Categories;

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = Category.NormalizeName(request.Search);

                query = query.Where(category =>
                    category.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            query = request.SortBy?.Trim().ToLowerInvariant() switch
            {
                "name" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(category => category.Name)
                    : query.OrderBy(category => category.Name),
                "isactive" or "is_active" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(category => category.IsActive)
                    : query.OrderBy(category => category.IsActive),
                "updatedat" or "updated_at" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(category => category.UpdatedAtUtc)
                    : query.OrderBy(category => category.UpdatedAtUtc),
                _ => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(category => category.CreatedAtUtc)
                    : query.OrderBy(category => category.CreatedAtUtc)
            };

            var categories = query.ToArray();
            var page = categories
                .Skip(request.Skip)
                .Take(request.PageSize)
                .ToArray();

            return Task.FromResult(new PaginatedResult<Category>(
                page,
                new PaginationMetadata(request.Page, request.PageSize, categories.Length)));
        }

        public Task<bool> NameExistsAsync(
            string normalizedName,
            Guid? excludingCategoryId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Categories.Any(category =>
                string.Equals(category.Name, normalizedName, StringComparison.OrdinalIgnoreCase) &&
                (!excludingCategoryId.HasValue || category.Id != excludingCategoryId.Value)));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            foreach (var category in Categories)
            {
                category.NormalizeNameForStorage();
            }

            return Task.CompletedTask;
        }
    }

    protected sealed class FakeSupplierRepository : ISupplierRepository
    {
        public List<Supplier> Suppliers { get; } = [];

        public Task AddAsync(Supplier supplier, CancellationToken cancellationToken)
        {
            Suppliers.Add(supplier);

            return Task.CompletedTask;
        }

        public Supplier CreateSupplier(
            string name,
            string? email = null,
            string? phone = null,
            string? address = null,
            bool isActive = true)
        {
            var supplier = new Supplier
            {
                Id = Guid.NewGuid(),
                Name = name,
                Email = email,
                Phone = phone,
                Address = address,
                IsActive = isActive,
                CreatedAtUtc = DateTime.UtcNow
            };
            supplier.TrimStringFieldsForStorage();

            return supplier;
        }

        public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Suppliers.SingleOrDefault(supplier => supplier.Id == id));
        }

        public Task<Supplier?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Suppliers.SingleOrDefault(supplier => supplier.Id == id));
        }

        public Task<PaginatedResult<Supplier>> ListAsync(
            SupplierListRequest request,
            CancellationToken cancellationToken)
        {
            IEnumerable<Supplier> query = Suppliers;

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim();

                query = query.Where(supplier =>
                    supplier.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (supplier.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (supplier.Phone?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            query = request.SortBy?.Trim().ToLowerInvariant() switch
            {
                "name" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(supplier => supplier.Name)
                    : query.OrderBy(supplier => supplier.Name),
                "email" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(supplier => supplier.Email)
                    : query.OrderBy(supplier => supplier.Email),
                "phone" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(supplier => supplier.Phone)
                    : query.OrderBy(supplier => supplier.Phone),
                "isactive" or "is_active" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(supplier => supplier.IsActive)
                    : query.OrderBy(supplier => supplier.IsActive),
                "updatedat" or "updated_at" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(supplier => supplier.UpdatedAtUtc)
                    : query.OrderBy(supplier => supplier.UpdatedAtUtc),
                _ => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(supplier => supplier.CreatedAtUtc)
                    : query.OrderBy(supplier => supplier.CreatedAtUtc)
            };

            var suppliers = query.ToArray();
            var page = suppliers
                .Skip(request.Skip)
                .Take(request.PageSize)
                .ToArray();

            return Task.FromResult(new PaginatedResult<Supplier>(
                page,
                new PaginationMetadata(request.Page, request.PageSize, suppliers.Length)));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            foreach (var supplier in Suppliers)
            {
                supplier.TrimStringFieldsForStorage();
            }

            return Task.CompletedTask;
        }
    }

    protected sealed class FakeProductRepository : IProductRepository
    {
        public Category ActiveCategory { get; } = new()
        {
            Id = Guid.NewGuid(),
            Name = "Hardware",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        public Category InactiveCategory { get; } = new()
        {
            Id = Guid.NewGuid(),
            Name = "Inactive Category",
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        public Supplier ActiveSupplier { get; } = new()
        {
            Id = Guid.NewGuid(),
            Name = "Main Supplier",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        public Supplier InactiveSupplier { get; } = new()
        {
            Id = Guid.NewGuid(),
            Name = "Inactive Supplier",
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        public List<Category> Categories { get; }

        public List<Supplier> Suppliers { get; }

        public List<Product> Products { get; } = [];

        public bool ThrowConcurrencyOnSave { get; init; }

        public FakeProductRepository()
        {
            Categories = [ActiveCategory, InactiveCategory];
            Suppliers = [ActiveSupplier, InactiveSupplier];
        }

        public Task AddAsync(Product product, CancellationToken cancellationToken)
        {
            Products.Add(product);

            return Task.CompletedTask;
        }

        public Category CreateCategory(string name, bool isActive)
        {
            return new Category
            {
                Id = Guid.NewGuid(),
                Name = name,
                IsActive = isActive,
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        public Product CreateProduct(
            string sku,
            string name,
            Category? category = null,
            Supplier? supplier = null,
            decimal price = 10m,
            int stock = 1,
            bool isActive = true)
        {
            var productCategory = category ?? ActiveCategory;
            var productSupplier = supplier ?? ActiveSupplier;
            var product = new Product
            {
                Id = Guid.NewGuid(),
                SKU = sku,
                Name = name,
                Unit = "pcs",
                Price = price,
                Stock = stock,
                CategoryId = productCategory.Id,
                Category = productCategory,
                SupplierId = productSupplier?.Id,
                Supplier = productSupplier,
                IsActive = isActive,
                CreatedAtUtc = DateTime.UtcNow,
                RowVersion = [1]
            };
            product.NormalizeForStorage();

            return product;
        }

        public Task<Category?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Categories.SingleOrDefault(category => category.Id == id));
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Products.SingleOrDefault(product => product.Id == id));
        }

        public Task<Product?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Products.SingleOrDefault(product => product.Id == id));
        }

        public Task<Supplier?> GetSupplierByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Suppliers.SingleOrDefault(supplier => supplier.Id == id));
        }

        public Task<PaginatedResult<Product>> ListAsync(ProductListRequest request, CancellationToken cancellationToken)
        {
            IEnumerable<Product> query = Products;

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim();
                var normalizedSkuSearch = Product.NormalizeSku(search);

                query = query.Where(product =>
                    product.SKU.Contains(normalizedSkuSearch, StringComparison.OrdinalIgnoreCase) ||
                    product.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (request.CategoryId.HasValue)
            {
                query = query.Where(product => product.CategoryId == request.CategoryId.Value);
            }

            if (request.SupplierId.HasValue)
            {
                query = query.Where(product => product.SupplierId == request.SupplierId.Value);
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(product => product.IsActive == request.IsActive.Value);
            }

            query = request.SortBy?.Trim().ToLowerInvariant() switch
            {
                "sku" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(product => product.SKU)
                    : query.OrderBy(product => product.SKU),
                "name" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(product => product.Name)
                    : query.OrderBy(product => product.Name),
                "price" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(product => product.Price)
                    : query.OrderBy(product => product.Price),
                "stock" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(product => product.Stock)
                    : query.OrderBy(product => product.Stock),
                "isactive" or "is_active" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(product => product.IsActive)
                    : query.OrderBy(product => product.IsActive),
                "updatedat" or "updated_at" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(product => product.UpdatedAtUtc)
                    : query.OrderBy(product => product.UpdatedAtUtc),
                _ => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(product => product.CreatedAtUtc)
                    : query.OrderBy(product => product.CreatedAtUtc)
            };

            var products = query.ToArray();
            var page = products
                .Skip(request.Skip)
                .Take(request.PageSize)
                .ToArray();

            return Task.FromResult(new PaginatedResult<Product>(
                page,
                new PaginationMetadata(request.Page, request.PageSize, products.Length)));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (ThrowConcurrencyOnSave)
            {
                throw new DbUpdateConcurrencyException();
            }

            foreach (var product in Products)
            {
                product.NormalizeForStorage();
            }

            return Task.CompletedTask;
        }

        public Task<bool> SkuExistsAsync(
            string normalizedSku,
            Guid? excludingProductId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Products.Any(product =>
                product.SKU == normalizedSku &&
                (!excludingProductId.HasValue || product.Id != excludingProductId.Value)));
        }
    }

    protected sealed class FakeCustomerRepository : ICustomerRepository
    {
        public List<Customer> Customers { get; } = [];

        public Task AddAsync(Customer customer, CancellationToken cancellationToken)
        {
            Customers.Add(customer);

            return Task.CompletedTask;
        }

        public Customer CreateCustomer(
            string name,
            string email,
            string? phone = null,
            string shippingAddress = "Main Street",
            bool isActive = true)
        {
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = name,
                Email = email,
                Phone = phone,
                ShippingAddress = shippingAddress,
                IsActive = isActive,
                CreatedAtUtc = DateTime.UtcNow
            };
            customer.NormalizeForStorage();

            return customer;
        }

        public Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingCustomerId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Customers.Any(customer =>
                customer.Email == normalizedEmail &&
                (!excludingCustomerId.HasValue || customer.Id != excludingCustomerId.Value)));
        }

        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Customers.SingleOrDefault(customer => customer.Id == id));
        }

        public Task<Customer?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Customers.SingleOrDefault(customer => customer.Id == id));
        }

        public Task<PaginatedResult<Customer>> ListAsync(CustomerListRequest request, CancellationToken cancellationToken)
        {
            IEnumerable<Customer> query = Customers;

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim();
                var normalizedEmailSearch = User.NormalizeEmail(search);

                query = query.Where(customer =>
                    customer.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    customer.Email.Contains(normalizedEmailSearch, StringComparison.OrdinalIgnoreCase));
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(customer => customer.IsActive == request.IsActive.Value);
            }

            query = request.SortBy?.Trim().ToLowerInvariant() switch
            {
                "name" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(customer => customer.Name)
                    : query.OrderBy(customer => customer.Name),
                "email" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(customer => customer.Email)
                    : query.OrderBy(customer => customer.Email),
                "isactive" or "is_active" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(customer => customer.IsActive)
                    : query.OrderBy(customer => customer.IsActive),
                "updatedat" or "updated_at" => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(customer => customer.UpdatedAtUtc)
                    : query.OrderBy(customer => customer.UpdatedAtUtc),
                _ => request.SortDirection == SortDirection.Desc
                    ? query.OrderByDescending(customer => customer.CreatedAtUtc)
                    : query.OrderBy(customer => customer.CreatedAtUtc)
            };

            var customers = query.ToArray();
            var page = customers
                .Skip(request.Skip)
                .Take(request.PageSize)
                .ToArray();

            return Task.FromResult(new PaginatedResult<Customer>(
                page,
                new PaginationMetadata(request.Page, request.PageSize, customers.Length)));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            foreach (var customer in Customers)
            {
                customer.NormalizeForStorage();
            }

            return Task.CompletedTask;
        }
    }

    protected sealed class FakeOrderRepository : IOrderRepository
    {
        public User CurrentUser { get; } = new()
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            FullName = "Admin User",
            PasswordHash = "hashed-admin-password",
            Role = new Role { Id = Guid.NewGuid(), Name = SystemRoleNames.Admin },
            IsActive = true
        };

        public User OtherUser { get; } = new()
        {
            Id = Guid.NewGuid(),
            Email = "other@example.com",
            FullName = "Other User",
            PasswordHash = "hashed-other-password",
            Role = new Role { Id = Guid.NewGuid(), Name = SystemRoleNames.SalesOperator },
            IsActive = true
        };

        public Customer ActiveCustomer { get; } = new()
        {
            Id = Guid.NewGuid(),
            Name = "Jane Buyer",
            Email = "jane@example.com",
            ShippingAddress = "Main Street",
            IsActive = true
        };

        public Customer InactiveCustomer { get; } = new()
        {
            Id = Guid.NewGuid(),
            Name = "Inactive Buyer",
            Email = "inactive@example.com",
            ShippingAddress = "Inactive Street",
            IsActive = false
        };

        public List<Product> Products { get; } =
        [
            new Product
            {
                Id = Guid.NewGuid(),
                SKU = "ABC-123",
                Name = "Hammer",
                Unit = "pcs",
                Price = 10m,
                Stock = 5,
                IsActive = true,
                RowVersion = [1]
            }
        ];

        public List<Order> Orders { get; } = [];

        public Guid? LastListScopeCreatedByUserId { get; private set; }

        public Guid? LastGetScopeCreatedByUserId { get; private set; }

        public bool UseInactiveCustomer { get; init; }

        public bool CustomerMissing { get; init; }

        public bool UseInactiveProduct { get; init; }

        public bool ProductMissing { get; init; }

        public bool ThrowOnSave { get; init; }

        public bool ThrowConcurrencyOnSave { get; init; }

        public bool TransactionCommitted { get; private set; }

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken)
        {
            var stockSnapshot = Products.ToDictionary(product => product.Id, product => product.Stock);
            var orderCount = Orders.Count;
            var orderSnapshots = Orders.ToDictionary(
                order => order.Id,
                order => new OrderSnapshot(
                    order.Status,
                    order.TrackingNumber,
                    order.CancelledAtUtc,
                    order.UpdatedAtUtc,
                    order.StatusHistory.Count));

            try
            {
                var result = await operation(cancellationToken);
                TransactionCommitted = true;

                return result;
            }
            catch
            {
                foreach (var product in Products)
                {
                    product.Stock = stockSnapshot[product.Id];
                }

                Orders.RemoveRange(orderCount, Orders.Count - orderCount);

                foreach (var order in Orders)
                {
                    if (!orderSnapshots.TryGetValue(order.Id, out var snapshot))
                    {
                        continue;
                    }

                    order.Status = snapshot.Status;
                    order.TrackingNumber = snapshot.TrackingNumber;
                    order.CancelledAtUtc = snapshot.CancelledAtUtc;
                    order.UpdatedAtUtc = snapshot.UpdatedAtUtc;

                    while (order.StatusHistory.Count > snapshot.HistoryCount)
                    {
                        order.StatusHistory.Remove(order.StatusHistory.Last());
                    }
                }

                throw;
            }
        }

        public Task AddAsync(Order order, CancellationToken cancellationToken)
        {
            Orders.Add(order);

            return Task.CompletedTask;
        }

        public Task<Customer?> GetCustomerForOrderAsync(Guid id, CancellationToken cancellationToken)
        {
            if (CustomerMissing)
            {
                return Task.FromResult<Customer?>(null);
            }

            var customer = UseInactiveCustomer ? InactiveCustomer : ActiveCustomer;

            return Task.FromResult<Customer?>(UseInactiveCustomer || customer.Id == id ? customer : null);
        }

        public Task<User?> GetCreatedByUserAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult<User?>(CurrentUser.Id == id ? CurrentUser : null);
        }

        public Task<IReadOnlyDictionary<Guid, Product>> GetProductsForOrderUpdateAsync(
            IReadOnlyCollection<Guid> productIds,
            CancellationToken cancellationToken)
        {
            if (ProductMissing)
            {
                return Task.FromResult<IReadOnlyDictionary<Guid, Product>>(new Dictionary<Guid, Product>());
            }

            if (UseInactiveProduct)
            {
                Products.Single().IsActive = false;
            }

            return Task.FromResult<IReadOnlyDictionary<Guid, Product>>(
                Products
                    .Where(product => productIds.Contains(product.Id))
                    .ToDictionary(product => product.Id));
        }

        public Task<PaginatedResult<Order>> ListAsync(
            Guid? createdByUserId,
            OrderQueryRequest request,
            CancellationToken cancellationToken)
        {
            LastListScopeCreatedByUserId = createdByUserId;
            var query = Orders.AsEnumerable();

            if (createdByUserId.HasValue)
            {
                query = query.Where(order => order.CreatedByUserId == createdByUserId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                var status = Enum.Parse<OrderStatus>(request.Status.Trim(), ignoreCase: true);

                query = query.Where(order => order.Status == status);
            }

            if (request.CustomerId.HasValue)
            {
                query = query.Where(order => order.CustomerId == request.CustomerId.Value);
            }

            if (request.DateFrom.HasValue)
            {
                var dateFromUtc = NormalizeUtc(request.DateFrom.Value);

                query = query.Where(order => order.CreatedAtUtc >= dateFromUtc);
            }

            if (request.DateTo.HasValue)
            {
                var dateToUtc = NormalizeUtc(request.DateTo.Value);

                query = query.Where(order => order.CreatedAtUtc <= dateToUtc);
            }

            var sortedOrders = ApplyOrderSorting(query, request.SortBy, request.SortDirection);
            var allOrders = sortedOrders.ToArray();
            var page = allOrders
                .Skip(request.Skip)
                .Take(request.PageSize)
                .ToArray();

            return Task.FromResult(new PaginatedResult<Order>(
                page,
                new PaginationMetadata(request.Page, request.PageSize, allOrders.Length)));
        }

        public Task<Order?> GetByIdAsync(
            Guid id,
            Guid? createdByUserId,
            CancellationToken cancellationToken)
        {
            LastGetScopeCreatedByUserId = createdByUserId;
            var order = Orders.SingleOrDefault(order =>
                order.Id == id &&
                (!createdByUserId.HasValue || order.CreatedByUserId == createdByUserId.Value));

            return Task.FromResult(order);
        }

        public Task<Order?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Orders.SingleOrDefault(order => order.Id == id));
        }

        public Task<Order?> GetByIdForCancellationAsync(
            Guid id,
            Guid? createdByUserId,
            CancellationToken cancellationToken)
        {
            var order = Orders.SingleOrDefault(order =>
                order.Id == id &&
                (!createdByUserId.HasValue || order.CreatedByUserId == createdByUserId.Value));

            return Task.FromResult(order);
        }

        public Task<IReadOnlyCollection<OrderStatusHistory>?> GetStatusHistoryAsync(
            Guid orderId,
            Guid? createdByUserId,
            CancellationToken cancellationToken)
        {
            var order = Orders.SingleOrDefault(order =>
                order.Id == orderId &&
                (!createdByUserId.HasValue || order.CreatedByUserId == createdByUserId.Value));

            return Task.FromResult<IReadOnlyCollection<OrderStatusHistory>?>(
                order?.StatusHistory
                    .OrderBy(history => history.ChangedAtUtc)
                    .ThenBy(history => history.Id)
                    .ToArray());
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (ThrowConcurrencyOnSave)
            {
                throw new DbUpdateConcurrencyException();
            }

            if (ThrowOnSave)
            {
                throw new InvalidOperationException("Simulated persistence failure.");
            }

            foreach (var order in Orders)
            {
                order.NormalizeForStorage();

                foreach (var item in order.Items)
                {
                    item.NormalizeForStorage();
                }

                foreach (var history in order.StatusHistory)
                {
                    history.NormalizeForStorage();
                }
            }

            return Task.CompletedTask;
        }

        public Order CreatePersistedOrder(
            Guid createdByUserId,
            string orderNumber,
            DateTime? createdAtUtc = null,
            OrderStatus status = OrderStatus.Pending,
            string? trackingNumber = "TRK-123",
            Guid? customerId = null,
            DateTime? updatedAtUtc = null,
            decimal totalAmount = 20m)
        {
            var createdByUser = createdByUserId == CurrentUser.Id ? CurrentUser : OtherUser;
            var product = Products.Single();
            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = orderNumber,
                CustomerId = customerId ?? ActiveCustomer.Id,
                Customer = ActiveCustomer,
                CreatedByUserId = createdByUserId,
                CreatedByUser = createdByUser,
                Status = status,
                TrackingNumber = trackingNumber,
                CurrencyCode = "IDR",
                ExchangeRate = null,
                Subtotal = totalAmount,
                TotalAmount = totalAmount,
                CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow,
                UpdatedAtUtc = updatedAtUtc
            };

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Product = product,
                ProductSku = product.SKU,
                ProductName = product.Name,
                Quantity = 2,
                UnitPrice = product.Price,
                LineTotal = 20m
            });
            Orders.Add(order);

            return order;
        }

        public Product AddProduct(
            string sku,
            string name,
            decimal price = 10m,
            int stock = 0)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                SKU = sku,
                Name = name,
                Unit = "pcs",
                Price = price,
                Stock = stock,
                IsActive = true,
                RowVersion = [1]
            };
            product.NormalizeForStorage();
            Products.Add(product);

            return product;
        }

        public void AddOrderItem(Order order, Product product, int quantity)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Product = product,
                ProductSku = product.SKU,
                ProductName = product.Name,
                Quantity = quantity,
                UnitPrice = product.Price,
                LineTotal = product.Price * quantity
            });
        }

        public OrderStatusHistory AddHistory(
            Order order,
            OrderStatus? fromStatus,
            OrderStatus toStatus,
            User? changedByUser = null,
            DateTime? changedAtUtc = null,
            string? note = null,
            Guid? id = null)
        {
            var actor = changedByUser ?? CurrentUser;
            var history = new OrderStatusHistory
            {
                Id = id ?? Guid.NewGuid(),
                OrderId = order.Id,
                Order = order,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                ChangedByUserId = actor.Id,
                ChangedByUser = actor,
                ChangedAtUtc = changedAtUtc ?? DateTime.UtcNow,
                Note = note
            };
            order.StatusHistory.Add(history);

            return history;
        }

        private static IOrderedEnumerable<Order> ApplyOrderSorting(
            IEnumerable<Order> query,
            string? sortBy,
            SortDirection sortDirection)
        {
            var descending = sortDirection == SortDirection.Desc;

            return NormalizeSortBy(sortBy) switch
            {
                "updatedat" => descending
                    ? query.OrderByDescending(order => order.UpdatedAtUtc).ThenByDescending(order => order.Id)
                    : query.OrderBy(order => order.UpdatedAtUtc).ThenBy(order => order.Id),
                "ordernumber" => descending
                    ? query.OrderByDescending(order => order.OrderNumber).ThenByDescending(order => order.Id)
                    : query.OrderBy(order => order.OrderNumber).ThenBy(order => order.Id),
                "status" => descending
                    ? query.OrderByDescending(order => order.Status).ThenByDescending(order => order.Id)
                    : query.OrderBy(order => order.Status).ThenBy(order => order.Id),
                "totalamount" => descending
                    ? query.OrderByDescending(order => order.TotalAmount).ThenByDescending(order => order.Id)
                    : query.OrderBy(order => order.TotalAmount).ThenBy(order => order.Id),
                _ => descending
                    ? query.OrderByDescending(order => order.CreatedAtUtc).ThenByDescending(order => order.Id)
                    : query.OrderBy(order => order.CreatedAtUtc).ThenBy(order => order.Id)
            };
        }

        private static string NormalizeSortBy(string? sortBy)
        {
            return string.IsNullOrWhiteSpace(sortBy)
                ? "createdat"
                : sortBy.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private sealed record OrderSnapshot(
            OrderStatus Status,
            string? TrackingNumber,
            DateTime? CancelledAtUtc,
            DateTime? UpdatedAtUtc,
            int HistoryCount);
    }

    protected sealed class FakeReportingRepository : IReportingRepository
    {
        private bool duplicateAttempted;

        public List<Order> Orders { get; } = [];

        public List<DailySalesReport> Reports { get; } = [];

        public List<BackgroundJobExecution> Executions { get; } = [];

        public bool ThrowOnListOrders { get; init; }

        public bool ThrowOnSaveReports { get; init; }

        public bool ThrowDuplicateOnReportAdd { get; init; }

        public bool HideExistingReportUntilDuplicate { get; set; }

        public DateTime? LastStartUtc { get; private set; }

        public DateTime? LastEndUtc { get; private set; }

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken)
        {
            var reportCount = Reports.Count;
            var executionCount = Executions.Count;

            try
            {
                return await operation(cancellationToken);
            }
            catch
            {
                Reports.RemoveRange(reportCount, Reports.Count - reportCount);
                Executions.RemoveRange(executionCount, Executions.Count - executionCount);
                throw;
            }
        }

        public Task<DailySalesReport?> GetDailySalesReportByDateAsync(
            DateOnly reportDate,
            CancellationToken cancellationToken)
        {
            return GetPersistedDailySalesReportByDateAsync(reportDate, cancellationToken);
        }

        public Task<DailySalesReport?> GetPersistedDailySalesReportByDateAsync(
            DateOnly reportDate,
            CancellationToken cancellationToken)
        {
            if (HideExistingReportUntilDuplicate && !duplicateAttempted)
            {
                return Task.FromResult<DailySalesReport?>(null);
            }

            return Task.FromResult(Reports.SingleOrDefault(report => report.ReportDate == reportDate));
        }

        public Task<IReadOnlyCollection<Order>> ListDeliveredOrdersForDateAsync(
            DateTime startUtc,
            DateTime endUtc,
            CancellationToken cancellationToken)
        {
            if (ThrowOnListOrders)
            {
                throw new InvalidOperationException("Simulated report query failure.");
            }

            LastStartUtc = startUtc;
            LastEndUtc = endUtc;

            return Task.FromResult<IReadOnlyCollection<Order>>(
                Orders
                    .Where(order =>
                        order.Status == OrderStatus.Delivered &&
                        order.CreatedAtUtc >= startUtc &&
                        order.CreatedAtUtc < endUtc)
                    .ToArray());
        }

        public Task AddDailySalesReportAsync(DailySalesReport report, CancellationToken cancellationToken)
        {
            if (ThrowDuplicateOnReportAdd)
            {
                duplicateAttempted = true;
                throw new DbUpdateException("Duplicate unique ReportDate.");
            }

            Reports.Add(report);

            return Task.CompletedTask;
        }

        public Task AddBackgroundJobExecutionAsync(
            BackgroundJobExecution execution,
            CancellationToken cancellationToken)
        {
            Executions.Add(execution);

            return Task.CompletedTask;
        }

        public void ClearChanges()
        {
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnSaveReports && Reports.Count > 0)
            {
                throw new InvalidOperationException("Simulated report persistence failure.");
            }

            return Task.CompletedTask;
        }

        public DailySalesReport AddExistingReport(
            DateOnly reportDate,
            int totalOrders,
            decimal totalRevenue)
        {
            var report = new DailySalesReport
            {
                Id = Guid.NewGuid(),
                ReportDate = reportDate,
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                GeneratedAtUtc = DateTime.UtcNow
            };
            Reports.Add(report);

            return report;
        }

        public Order AddOrder(
            OrderStatus status,
            DateTime createdAtUtc,
            decimal totalAmount)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = $"ORD-{Orders.Count + 1:D4}",
                CustomerId = Guid.NewGuid(),
                CreatedByUserId = Guid.NewGuid(),
                Status = status,
                CurrencyCode = "IDR",
                Subtotal = totalAmount,
                TotalAmount = totalAmount,
                CreatedAtUtc = createdAtUtc
            };
            Orders.Add(order);

            return order;
        }

        public void AddItem(
            Order order,
            string productSku,
            string productName,
            int quantity,
            decimal lineTotal)
        {
            AddItem(order, Guid.NewGuid(), productSku, productName, quantity, lineTotal);
        }

        public void AddItem(
            Order order,
            Guid productId,
            string productSku,
            string productName,
            int quantity,
            decimal lineTotal)
        {
            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Order = order,
                ProductId = productId,
                ProductSku = productSku,
                ProductName = productName,
                Quantity = quantity,
                UnitPrice = quantity == 0 ? 0 : lineTotal / quantity,
                LineTotal = lineTotal
            });
        }
    }

    protected sealed class FakeDailySalesReportGenerator : IDailySalesReportGenerator
    {
        public int CallCount { get; private set; }

        public DateOnly? LastReportDate { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public bool ThrowOnGenerate { get; init; }

        public Task<DailySalesReportResponse> GenerateAsync(
            DateOnly reportDate,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastReportDate = reportDate;
            LastCancellationToken = cancellationToken;

            if (ThrowOnGenerate)
            {
                throw new InvalidOperationException("Simulated generator failure.");
            }

            return Task.FromResult(new DailySalesReportResponse(
                Guid.NewGuid(),
                reportDate,
                0,
                0m,
                DateTime.UtcNow,
                []));
        }
    }

    protected sealed class FakeExchangeRateClient : IExchangeRateClient
    {
        public int CallCount { get; private set; }

        public Task<ExchangeRateResult> GetLatestRateAsync(
            string fromCurrency,
            string toCurrency,
            CancellationToken cancellationToken)
        {
            CallCount++;

            return Task.FromResult(new ExchangeRateResult(
                fromCurrency,
                toCurrency,
                16000m,
                "Frankfurter",
                new DateOnly(2026, 7, 17),
                DateTime.UtcNow));
        }
    }

    protected sealed class FakeExchangeRateService(decimal rate = 16000m, bool throwExternalFailure = false) : ICurrencyService
    {
        public Task<ExchangeRateResponse> GetExchangeRateAsync(
            string fromCurrency,
            string toCurrency,
            CancellationToken cancellationToken)
        {
            if (throwExternalFailure)
            {
                throw new ExternalServiceException("Currency exchange service is unavailable.");
            }

            return Task.FromResult(new ExchangeRateResponse(
                CurrencyCode.Normalize(fromCurrency),
                CurrencyCode.Normalize(toCurrency),
                rate,
                "Frankfurter",
                new DateOnly(2026, 7, 17),
                DateTime.UtcNow));
        }
    }

    protected sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<CancellationToken> CancellationTokens { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            CancellationTokens.Add(cancellationToken);

            return Task.FromResult(send(request));
        }
    }

    protected sealed class FakeCoravelQueue : IQueue
    {
        public object? LastPayload { get; private set; }

        public Type? LastInvocableType { get; private set; }

        public Guid QueueTask(Action task)
        {
            return Guid.NewGuid();
        }

        public Guid QueueAsyncTask(Func<Task> task)
        {
            return Guid.NewGuid();
        }

        public Guid QueueInvocable<T>()
            where T : Coravel.Invocable.IInvocable
        {
            LastInvocableType = typeof(T);

            return Guid.NewGuid();
        }

        public (Guid, CancellationTokenSource) QueueCancellableInvocable<T>()
            where T : Coravel.Invocable.IInvocable, Coravel.Queuing.Interfaces.ICancellableTask
        {
            LastInvocableType = typeof(T);

            return (Guid.NewGuid(), new CancellationTokenSource());
        }

        public void QueueBroadcast<TEvent>(TEvent broadcasted)
            where TEvent : Coravel.Events.Interfaces.IEvent
        {
        }

        public Guid QueueInvocableWithPayload<T, TParams>(TParams payload)
            where T : Coravel.Invocable.IInvocable, Coravel.Invocable.IInvocableWithPayload<TParams>
        {
            LastInvocableType = typeof(T);
            LastPayload = payload;

            return Guid.NewGuid();
        }

        public QueueMetrics GetMetrics()
        {
            return new QueueMetrics(waitingCount: 0, runningCount: 0);
        }
    }

    protected sealed class FakeOrderNumberGenerator : IOrderNumberGenerator
    {
        public string Create(DateTime createdAtUtc)
        {
            return "ORD-TEST-0001";
        }
    }

    protected sealed class FakeOrderStatusNotificationQueue(Func<bool>? hasCommitted = null) : IOrderStatusNotificationQueue
    {
        public int CallCount { get; private set; }

        public bool ThrowOnEnqueue { get; init; }

        public bool? WasCommittedWhenCalled { get; private set; }

        public OrderStatus? LastFromStatus { get; private set; }

        public OrderStatus? LastToStatus { get; private set; }

        public Task EnqueueStatusChangedAsync(
            Guid orderId,
            string orderNumber,
            OrderStatus fromStatus,
            OrderStatus toStatus,
            Guid changedByUserId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            WasCommittedWhenCalled = hasCommitted?.Invoke();
            LastFromStatus = fromStatus;
            LastToStatus = toStatus;

            if (ThrowOnEnqueue)
            {
                throw new InvalidOperationException("Simulated notification failure.");
            }

            return Task.CompletedTask;
        }
    }

    protected sealed class FakeCurrentUserContext(Guid? userId = null, string? role = null) : ICurrentUserContext
    {
        public bool IsAuthenticated => userId.HasValue;

        public Guid? UserId => userId;

        public string? Email => userId.HasValue ? "admin@example.com" : null;

        public string? FullName => userId.HasValue ? "Admin User" : null;

        public string? Role => role;

        public Guid GetRequiredUserId()
        {
            return userId ?? throw new InvalidOperationException("An authenticated user ID is required.");
        }
    }

    protected sealed class FakeUserService : IUserService
    {
        public static Guid UserId { get; } = Guid.NewGuid();

        private static UserResponse User => new(
            UserId,
            "admin@example.com",
            "Admin User",
            SystemRoleNames.Admin,
            true,
            DateTime.UtcNow,
            null);

        public Task<UserResponse> CreateAsync(
            CreateUserRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(User);
        }

        public Task<UserResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(User);
        }

        public Task<PaginatedResult<UserResponse>> ListAsync(
            UserListRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new PaginatedResult<UserResponse>(
                [User],
                new PaginationMetadata(1, 20, 1)));
        }

        public Task<UserResponse> UpdateAsync(
            Guid id,
            UpdateUserRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(User);
        }

        public Task<UserResponse> UpdateRoleAsync(
            Guid id,
            UpdateUserRoleRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(User);
        }

        public Task<UserResponse> UpdateStatusAsync(
            Guid id,
            UpdateUserStatusRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(User);
        }
    }

    protected sealed class FakeCategoryService : ICategoryService
    {
        public static Guid CategoryId { get; } = Guid.NewGuid();

        private static CategoryResponse Category => new(
            CategoryId,
            "Hardware",
            "Hardware and equipment",
            true,
            DateTime.UtcNow,
            null);

        public Task<CategoryResponse> CreateAsync(
            CreateCategoryRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Category);
        }

        public Task<CategoryResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Category);
        }

        public Task<PaginatedResult<CategoryResponse>> ListAsync(
            CategoryListRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new PaginatedResult<CategoryResponse>(
                [Category],
                new PaginationMetadata(1, 20, 1)));
        }

        public Task<CategoryResponse> UpdateAsync(
            Guid id,
            UpdateCategoryRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Category);
        }

        public Task<CategoryResponse> UpdateStatusAsync(
            Guid id,
            UpdateCategoryStatusRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Category with { IsActive = request.IsActive });
        }
    }

    protected sealed class FakeSupplierService : ISupplierService
    {
        public static Guid SupplierId { get; } = Guid.NewGuid();

        private static SupplierResponse Supplier => new(
            SupplierId,
            "Main Supplier",
            "supplier@example.com",
            "12345",
            "Warehouse",
            true,
            DateTime.UtcNow,
            null);

        public Task<SupplierResponse> CreateAsync(
            CreateSupplierRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Supplier);
        }

        public Task<SupplierResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Supplier);
        }

        public Task<PaginatedResult<SupplierResponse>> ListAsync(
            SupplierListRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new PaginatedResult<SupplierResponse>(
                [Supplier],
                new PaginationMetadata(1, 20, 1)));
        }

        public Task<SupplierResponse> UpdateAsync(
            Guid id,
            UpdateSupplierRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Supplier);
        }

        public Task<SupplierResponse> UpdateStatusAsync(
            Guid id,
            UpdateSupplierStatusRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Supplier with { IsActive = request.IsActive });
        }
    }

    protected sealed class FakeProductService : IProductService
    {
        public static Guid ProductId { get; } = Guid.NewGuid();

        private static ProductResponse Product => new(
            ProductId,
            "ABC-123",
            "Hammer",
            "pcs",
            10m,
            5,
            new ProductRelatedResourceResponse(Guid.NewGuid(), "Hardware"),
            new ProductRelatedResourceResponse(Guid.NewGuid(), "Main Supplier"),
            true,
            DateTime.UtcNow,
            null);

        public Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Product);
        }

        public Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Product);
        }

        public Task<PaginatedResult<ProductResponse>> ListAsync(ProductListRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PaginatedResult<ProductResponse>(
                [Product],
                new PaginationMetadata(1, 20, 1)));
        }

        public Task<ProductResponse> UpdateAsync(
            Guid id,
            UpdateProductRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Product);
        }

        public Task<ProductResponse> UpdateStatusAsync(
            Guid id,
            UpdateProductStatusRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Product with { IsActive = request.IsActive });
        }
    }

    protected sealed class FakeCustomerService : ICustomerService
    {
        public static Guid CustomerId { get; } = Guid.NewGuid();

        private static CustomerResponse Customer => new(
            CustomerId,
            "Jane Buyer",
            "jane@example.com",
            "12345",
            "Main Street",
            true,
            DateTime.UtcNow,
            null);

        public Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Customer);
        }

        public Task<CustomerResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Customer);
        }

        public Task<PaginatedResult<CustomerResponse>> ListAsync(CustomerListRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PaginatedResult<CustomerResponse>(
                [Customer],
                new PaginationMetadata(1, 20, 1)));
        }

        public Task<CustomerResponse> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Customer);
        }

        public Task<CustomerResponse> UpdateStatusAsync(Guid id, UpdateCustomerStatusRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Customer with { IsActive = request.IsActive });
        }
    }

    protected sealed class FakeOrderService : IOrderService
    {
        public static Guid CustomerId { get; } = Guid.NewGuid();

        public static Guid CreatedByUserId { get; } = Guid.NewGuid();

        public static Guid ProductId { get; } = Guid.NewGuid();

        public Task<PaginatedResult<OrderResponse>> ListAsync(
            OrderQueryRequest request,
            CancellationToken cancellationToken)
        {
            var order = CreateResponse();

            return Task.FromResult(new PaginatedResult<OrderResponse>(
                [order],
                new PaginationMetadata(request.Page, request.PageSize, 1)));
        }

        public Task<OrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateResponse() with { Id = id });
        }

        public Task<IReadOnlyCollection<OrderStatusHistoryResponse>> GetStatusHistoryAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<OrderStatusHistoryResponse>>(
                [
                    new OrderStatusHistoryResponse(
                        Guid.NewGuid(),
                        null,
                        OrderStatus.Pending,
                        null,
                        DateTime.UtcNow,
                        new OrderHistoryActorResponse(
                            CreatedByUserId,
                            "Admin User",
                            "admin@example.com",
                            SystemRoleNames.Admin))
                ]);
        }

        public Task<OrderResponse> ApproveAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateResponse() with { Id = id, Status = OrderStatus.Processing });
        }

        public Task<OrderResponse> ShipAsync(
            Guid id,
            ShipOrderRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateResponse() with
            {
                Id = id,
                Status = OrderStatus.Shipped,
                TrackingNumber = request.TrackingNumber.Trim()
            });
        }

        public Task<OrderResponse> DeliverAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateResponse() with
            {
                Id = id,
                Status = OrderStatus.Delivered,
                TrackingNumber = "JNE-123456"
            });
        }

        public Task<OrderResponse> CancelAsync(
            Guid id,
            CancelOrderRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateResponse() with
            {
                Id = id,
                Status = OrderStatus.Cancelled,
                CancelledAtUtc = DateTime.UtcNow
            });
        }

        public Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateResponse());
        }

        private static OrderResponse CreateResponse()
        {
            return new OrderResponse(
                Guid.NewGuid(),
                "ORD-TEST-0001",
                new OrderRelatedResourceResponse(CustomerId, "Jane Buyer"),
                new OrderRelatedResourceResponse(CreatedByUserId, "Admin User"),
                OrderStatus.Pending,
                null,
                "IDR",
                null,
                20m,
                20m,
                DateTime.UtcNow,
                null,
                null,
                [
                    new OrderItemResponse(
                        ProductId,
                        "ABC-123",
                        "Hammer",
                        2,
                        10m,
                        20m)
                ]);
        }
    }

    protected sealed class NoOpDatabaseInitializer : IDatabaseInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    protected class TestApplicationFactory : WebApplicationFactory<Program>
    {
        public static JwtOptions JwtOptions => new()
        {
            Issuer = "OMS.API.Tests",
            Audience = "OMS.API.Tests.Client",
            SigningKey = "test-host-signing-key-with-32-bytes-min",
            ExpirationMinutes = 30
        };

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddConsole();
            });
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost,1433;Database=OMS.Tests;TrustServerCertificate=True;",
                    ["Jwt:Issuer"] = JwtOptions.Issuer,
                    ["Jwt:Audience"] = JwtOptions.Audience,
                    ["Jwt:SigningKey"] = JwtOptions.SigningKey,
                    ["Jwt:ExpirationMinutes"] = JwtOptions.ExpirationMinutes.ToString()
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDatabaseInitializer>();
                services.AddSingleton<IDatabaseInitializer, NoOpDatabaseInitializer>();
            });
        }
    }

    protected sealed class UserManagementApplicationFactory : TestApplicationFactory
    {
        public static Guid UserId => FakeUserService.UserId;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserService>();
                services.AddSingleton<IUserService, FakeUserService>();
            });
        }
    }

    protected sealed class CategoryApplicationFactory : TestApplicationFactory
    {
        public static Guid CategoryId => FakeCategoryService.CategoryId;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICategoryService>();
                services.AddSingleton<ICategoryService, FakeCategoryService>();
            });
        }
    }

    protected sealed class SupplierApplicationFactory : TestApplicationFactory
    {
        public static Guid SupplierId => FakeSupplierService.SupplierId;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISupplierService>();
                services.AddSingleton<ISupplierService, FakeSupplierService>();
            });
        }
    }

    protected sealed class ProductApplicationFactory : TestApplicationFactory
    {
        public static Guid ProductId => FakeProductService.ProductId;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProductService>();
                services.AddSingleton<IProductService, FakeProductService>();
            });
        }
    }

    protected sealed class CustomerApplicationFactory : TestApplicationFactory
    {
        public static Guid CustomerId => FakeCustomerService.CustomerId;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICustomerService>();
                services.AddSingleton<ICustomerService, FakeCustomerService>();
            });
        }
    }

    protected sealed class OrderApplicationFactory : TestApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOrderService>();
                services.AddSingleton<IOrderService, FakeOrderService>();
            });
        }
    }

    protected sealed class ExchangeRateApplicationFactory : TestApplicationFactory
    {
        public bool ThrowExternalFailure { get; init; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICurrencyService>();
                services.AddSingleton<ICurrencyService>(
                    new FakeExchangeRateService(throwExternalFailure: ThrowExternalFailure));
            });
        }
    }

    protected sealed class IntegrationApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        public bool ThrowExternalFailure { get; init; }

        public IntegrationApplicationFactory()
        {
            ClearIntegrationLogs();
            connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddProvider(new IntegrationLogProvider());
            });
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                    ["Jwt:Issuer"] = TestApplicationFactory.JwtOptions.Issuer,
                    ["Jwt:Audience"] = TestApplicationFactory.JwtOptions.Audience,
                    ["Jwt:SigningKey"] = TestApplicationFactory.JwtOptions.SigningKey,
                    ["Jwt:ExpirationMinutes"] = TestApplicationFactory.JwtOptions.ExpirationMinutes.ToString(),
                    ["Frankfurter:BaseUrl"] = "https://example.invalid/",
                    ["Frankfurter:TimeoutSeconds"] = "2",
                    ["Frankfurter:RetryCount"] = "0"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
                services.RemoveAll<IDatabaseInitializer>();
                services.AddScoped<IDatabaseInitializer, IntegrationDatabaseInitializer>();
                services.RemoveAll<IExchangeRateClient>();
                services.AddSingleton<IExchangeRateClient>(_ => new IntegrationExchangeRateClient(ThrowExternalFailure));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                connection.Dispose();
            }
        }
    }

    protected sealed class IntegrationDatabaseInitializer(ApplicationDbContext dbContext) : IDatabaseInitializer
    {
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
            await CreateSqliteSchemaAsync(cancellationToken);

            var roles = new[]
            {
                new Role { Id = IntegrationSeedData.AdminRoleId, Name = SystemRoleNames.Admin },
                new Role { Id = IntegrationSeedData.SupervisorRoleId, Name = SystemRoleNames.Supervisor },
                new Role { Id = IntegrationSeedData.SalesRoleId, Name = SystemRoleNames.SalesOperator }
            };
            var passwordHash = new BCryptPasswordHasher().HashPassword(IntegrationTestPassword);

            dbContext.Roles.AddRange(roles);
            dbContext.Users.AddRange(
                new User
                {
                    Id = IntegrationSeedData.AdminUserId,
                    Email = "admin@example.com",
                    PasswordHash = passwordHash,
                    FullName = "Admin User",
                    RoleId = IntegrationSeedData.AdminRoleId
                },
                new User
                {
                    Id = IntegrationSeedData.SupervisorUserId,
                    Email = "supervisor@example.com",
                    PasswordHash = passwordHash,
                    FullName = "Supervisor User",
                    RoleId = IntegrationSeedData.SupervisorRoleId
                },
                new User
                {
                    Id = IntegrationSeedData.SalesUserId,
                    Email = "sales1@example.com",
                    PasswordHash = passwordHash,
                    FullName = "Sales One",
                    RoleId = IntegrationSeedData.SalesRoleId
                },
                new User
                {
                    Id = IntegrationSeedData.OtherSalesUserId,
                    Email = "sales2@example.com",
                    PasswordHash = passwordHash,
                    FullName = "Sales Two",
                    RoleId = IntegrationSeedData.SalesRoleId
                });
            dbContext.Categories.Add(new Category
            {
                Id = IntegrationSeedData.CategoryId,
                Name = "Seed Category",
                Description = "Seeded integration category."
            });
            dbContext.Suppliers.Add(new Supplier
            {
                Id = IntegrationSeedData.SupplierId,
                Name = "Seed Supplier",
                Email = "supplier@example.com"
            });
            dbContext.Customers.Add(new Customer
            {
                Id = IntegrationSeedData.CustomerId,
                Name = "Seed Customer",
                Email = "customer@example.com",
                ShippingAddress = "Seed Address"
            });
            dbContext.Products.AddRange(
                new Product
                {
                    Id = IntegrationSeedData.ProductId,
                    SKU = "SEED-001",
                    Name = "Seed Product",
                    Unit = "pcs",
                    Price = 10m,
                    Stock = 10,
                    CategoryId = IntegrationSeedData.CategoryId,
                    SupplierId = IntegrationSeedData.SupplierId,
                    RowVersion = [1]
                },
                new Product
                {
                    Id = IntegrationSeedData.SecondProductId,
                    SKU = "SEED-002",
                    Name = "Second Seed Product",
                    Unit = "pcs",
                    Price = 15m,
                    Stock = 10,
                    CategoryId = IntegrationSeedData.CategoryId,
                    SupplierId = IntegrationSeedData.SupplierId,
                    RowVersion = [1]
                });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task CreateSqliteSchemaAsync(CancellationToken cancellationToken)
        {
            string[] statements =
            [
                "DROP TABLE IF EXISTS DailySalesReportItems",
                "DROP TABLE IF EXISTS DailySalesReports",
                "DROP TABLE IF EXISTS BackgroundJobExecutions",
                "DROP TABLE IF EXISTS OrderStatusHistories",
                "DROP TABLE IF EXISTS OrderItems",
                "DROP TABLE IF EXISTS Orders",
                "DROP TABLE IF EXISTS Products",
                "DROP TABLE IF EXISTS Customers",
                "DROP TABLE IF EXISTS Suppliers",
                "DROP TABLE IF EXISTS Categories",
                "DROP TABLE IF EXISTS Users",
                "DROP TABLE IF EXISTS Roles",
                """
                CREATE TABLE Roles (
                    Id TEXT NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
                    Name TEXT NOT NULL
                )
                """,
                """
                CREATE TABLE Users (
                    Id TEXT NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
                    Email TEXT NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    FullName TEXT NOT NULL,
                    RoleId TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NULL,
                    IsActive INTEGER NOT NULL
                )
                """,
                """
                CREATE TABLE Categories (
                    Id TEXT NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Description TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NULL,
                    IsActive INTEGER NOT NULL
                )
                """,
                """
                CREATE TABLE Suppliers (
                    Id TEXT NOT NULL CONSTRAINT PK_Suppliers PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Email TEXT NULL,
                    Phone TEXT NULL,
                    Address TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NULL,
                    IsActive INTEGER NOT NULL
                )
                """,
                """
                CREATE TABLE Customers (
                    Id TEXT NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Email TEXT NOT NULL,
                    Phone TEXT NULL,
                    ShippingAddress TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NULL,
                    IsActive INTEGER NOT NULL
                )
                """,
                """
                CREATE TABLE Products (
                    Id TEXT NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
                    SKU TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Unit TEXT NOT NULL,
                    Price TEXT NOT NULL,
                    Stock INTEGER NOT NULL,
                    CategoryId TEXT NOT NULL,
                    SupplierId TEXT NULL,
                    RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NULL,
                    IsActive INTEGER NOT NULL
                )
                """,
                """
                CREATE TABLE Orders (
                    Id TEXT NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
                    OrderNumber TEXT NOT NULL,
                    CustomerId TEXT NOT NULL,
                    CreatedByUserId TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    TrackingNumber TEXT NULL,
                    CurrencyCode TEXT NOT NULL,
                    ExchangeRate TEXT NULL,
                    Subtotal TEXT NOT NULL,
                    TotalAmount TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NULL,
                    CancelledAtUtc TEXT NULL
                )
                """,
                """
                CREATE TABLE OrderItems (
                    Id TEXT NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY,
                    OrderId TEXT NOT NULL,
                    ProductId TEXT NOT NULL,
                    ProductSku TEXT NOT NULL,
                    ProductName TEXT NOT NULL,
                    Quantity INTEGER NOT NULL,
                    UnitPrice TEXT NOT NULL,
                    LineTotal TEXT NOT NULL
                )
                """,
                """
                CREATE TABLE OrderStatusHistories (
                    Id TEXT NOT NULL CONSTRAINT PK_OrderStatusHistories PRIMARY KEY,
                    OrderId TEXT NOT NULL,
                    FromStatus TEXT NULL,
                    ToStatus TEXT NOT NULL,
                    ChangedByUserId TEXT NOT NULL,
                    Note TEXT NULL,
                    ChangedAtUtc TEXT NOT NULL
                )
                """,
                """
                CREATE TABLE DailySalesReports (
                    Id TEXT NOT NULL CONSTRAINT PK_DailySalesReports PRIMARY KEY,
                    ReportDate TEXT NOT NULL,
                    TotalOrders INTEGER NOT NULL,
                    TotalRevenue TEXT NOT NULL,
                    GeneratedAtUtc TEXT NOT NULL
                )
                """,
                """
                CREATE TABLE DailySalesReportItems (
                    Id TEXT NOT NULL CONSTRAINT PK_DailySalesReportItems PRIMARY KEY,
                    DailySalesReportId TEXT NOT NULL,
                    ProductId TEXT NOT NULL,
                    ProductSku TEXT NOT NULL,
                    ProductName TEXT NOT NULL,
                    QuantitySold INTEGER NOT NULL,
                    Revenue TEXT NOT NULL
                )
                """,
                """
                CREATE TABLE BackgroundJobExecutions (
                    Id TEXT NOT NULL CONSTRAINT PK_BackgroundJobExecutions PRIMARY KEY,
                    JobName TEXT NOT NULL,
                    StartedAtUtc TEXT NOT NULL,
                    FinishedAtUtc TEXT NULL,
                    Status TEXT NOT NULL,
                    Message TEXT NULL
                )
                """,
                "CREATE UNIQUE INDEX IX_Roles_Name ON Roles (Name)",
                "CREATE UNIQUE INDEX IX_Users_Email ON Users (Email)",
                "CREATE UNIQUE INDEX IX_Categories_Name ON Categories (Name)",
                "CREATE UNIQUE INDEX IX_Customers_Email ON Customers (Email)",
                "CREATE UNIQUE INDEX IX_Products_SKU ON Products (SKU)",
                "CREATE UNIQUE INDEX IX_Orders_OrderNumber ON Orders (OrderNumber)",
                "CREATE INDEX IX_Orders_Status ON Orders (Status)",
                "CREATE INDEX IX_Orders_CustomerId ON Orders (CustomerId)",
                "CREATE INDEX IX_Orders_CreatedByUserId ON Orders (CreatedByUserId)",
                "CREATE INDEX IX_Orders_CreatedAtUtc ON Orders (CreatedAtUtc)",
                "CREATE INDEX IX_OrderItems_OrderId ON OrderItems (OrderId)",
                "CREATE INDEX IX_OrderItems_ProductId ON OrderItems (ProductId)",
                "CREATE INDEX IX_OrderStatusHistories_OrderId ON OrderStatusHistories (OrderId)",
                "CREATE UNIQUE INDEX IX_DailySalesReports_ReportDate ON DailySalesReports (ReportDate)",
                "CREATE INDEX IX_BackgroundJobExecutions_JobName ON BackgroundJobExecutions (JobName)",
                "CREATE INDEX IX_BackgroundJobExecutions_StartedAtUtc ON BackgroundJobExecutions (StartedAtUtc)",
                "CREATE INDEX IX_BackgroundJobExecutions_Status ON BackgroundJobExecutions (Status)"
            ];

            foreach (var statement in statements)
            {
                await dbContext.Database.ExecuteSqlRawAsync(statement, cancellationToken);
            }
        }
    }

    protected sealed class IntegrationExchangeRateClient(bool throwExternalFailure) : IExchangeRateClient
    {
        public Task<ExchangeRateResult> GetLatestRateAsync(
            string fromCurrency,
            string toCurrency,
            CancellationToken cancellationToken)
        {
            if (throwExternalFailure)
            {
                throw new ExternalServiceException("Currency exchange service is unavailable.");
            }

            return Task.FromResult(new ExchangeRateResult(
                CurrencyCode.Normalize(fromCurrency),
                CurrencyCode.Normalize(toCurrency),
                16000m,
                "IntegrationFake",
                new DateOnly(2026, 7, 17),
                DateTime.UtcNow));
        }
    }

    protected static class IntegrationSeedData
    {
        public static readonly Guid AdminRoleId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public static readonly Guid SupervisorRoleId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        public static readonly Guid SalesRoleId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        public static readonly Guid AdminUserId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        public static readonly Guid SupervisorUserId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public static readonly Guid SalesUserId = Guid.Parse("20000000-0000-0000-0000-000000000003");
        public static readonly Guid OtherSalesUserId = Guid.Parse("20000000-0000-0000-0000-000000000004");
        public static readonly Guid CategoryId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        public static readonly Guid SupplierId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        public static readonly Guid CustomerId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        public static readonly Guid ProductId = Guid.Parse("60000000-0000-0000-0000-000000000001");
        public static readonly Guid SecondProductId = Guid.Parse("60000000-0000-0000-0000-000000000002");
    }

    protected const string IntegrationTestPassword = "StrongPassword123!";

    protected sealed class IntegrationLogProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new IntegrationLogger(categoryName);

        public void Dispose()
        {
        }
    }

    protected sealed class IntegrationLogger(string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            lock (IntegrationLogsLock)
            {
                IntegrationLogs.Add($"{logLevel} {categoryName}: {formatter(state, exception)} {exception}");
            }
        }
    }

    protected static readonly List<string> IntegrationLogs = [];

    protected static readonly object IntegrationLogsLock = new();

    protected sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var stateValues = state as IReadOnlyList<KeyValuePair<string, object?>>;

            Entries.Add(new LogEntry(
                logLevel,
                formatter(state, exception),
                stateValues?.Where(pair => pair.Key != "{OriginalFormat}")
                    .ToDictionary(pair => pair.Key, pair => pair.Value)
                    ?? []));
        }
    }

    protected sealed record LogEntry(
        LogLevel LogLevel,
        string Message,
        IReadOnlyDictionary<string, object?> State);

    protected static JwtOptions CreateValidJwtOptions(int expirationMinutes)
    {
        return new JwtOptions
        {
            Issuer = "OMS.API.Tests",
            Audience = "OMS.API.Tests.Client",
            SigningKey = "unit-test-signing-key-with-32-bytes-minimum",
            ExpirationMinutes = expirationMinutes
        };
    }

    protected static ClaimsPrincipal ValidateToken(
        string accessToken,
        JwtOptions options,
        out JwtSecurityToken validatedToken)
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(accessToken, validationParameters, out var securityToken);

        validatedToken = Assert.IsType<JwtSecurityToken>(securityToken);

        return principal;
    }

    protected static AuthService CreateAuthService(
        FakeAuthRepository repository,
        IPasswordHasher? passwordHasher = null)
    {
        return new AuthService(
            repository,
            passwordHasher ?? new BCryptPasswordHasher(),
            new JwtTokenService(Options.Create(CreateValidJwtOptions(expirationMinutes: 30))));
    }

    protected static UserService CreateUserService(
        FakeUserRepository repository,
        IPasswordHasher? passwordHasher = null,
        ICurrentUserContext? currentUser = null)
    {
        return new UserService(
            repository,
            passwordHasher ?? new BCryptPasswordHasher(),
            currentUser ?? new FakeCurrentUserContext(Guid.NewGuid(), SystemRoleNames.Admin));
    }

    protected static OrderService CreateOrderService(
        FakeOrderRepository repository,
        string roleName = SystemRoleNames.Admin,
        Guid? userId = null,
        IOrderStatusNotificationQueue? notificationQueue = null,
        ICurrencyService? exchangeRateService = null)
    {
        return new OrderService(
            repository,
            new FakeCurrentUserContext(userId ?? repository.CurrentUser.Id, roleName),
            new FakeOrderNumberGenerator(),
            notificationQueue ?? new FakeOrderStatusNotificationQueue(),
            exchangeRateService ?? new FakeExchangeRateService(),
            new OrderCurrencyOptions(),
            NullLogger<OrderService>.Instance);
    }

    protected static DailySalesReportGenerator CreateDailySalesReportGenerator(FakeReportingRepository repository)
    {
        return new DailySalesReportGenerator(
            repository,
            NullLogger<DailySalesReportGenerator>.Instance);
    }

    protected static FrankfurterExchangeRateClient CreateFrankfurterClient(
        HttpMessageHandler handler,
        int retryCount = 0)
    {
        var options = new FrankfurterOptions
        {
            BaseUrl = "https://api.frankfurter.app/",
            TimeoutSeconds = 5,
            RetryCount = retryCount
        };
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.BaseUrl),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };

        return new FrankfurterExchangeRateClient(
            httpClient,
            NullLogger<FrankfurterExchangeRateClient>.Instance);
    }

    protected static OrderStatus NextStatus(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.Pending => OrderStatus.Processing,
            OrderStatus.Processing => OrderStatus.Shipped,
            OrderStatus.Shipped => OrderStatus.Delivered,
            OrderStatus.Delivered => OrderStatus.Cancelled,
            _ => OrderStatus.Pending
        };
    }

    protected static CreateOrderRequest CreateValidOrderRequest()
    {
        return new CreateOrderRequest(
            FakeOrderService.CustomerId,
            "IDR",
            [new CreateOrderItemRequest(FakeOrderService.ProductId, 2)]);
    }

    protected static CreateOrderRequest CreateValidOrderRequest(FakeOrderRepository repository)
    {
        return new CreateOrderRequest(
            repository.ActiveCustomer.Id,
            "IDR",
            [new CreateOrderItemRequest(repository.Products.Single().Id, 2)]);
    }

    protected static StringContent CreateJsonContent<T>(T value)
    {
        return new StringContent(
            JsonSerializer.Serialize(value),
            Encoding.UTF8,
            "application/json");
    }

    protected static async Task<string> LoginForTokenAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsync(
            "/api/v1/auth/login",
            CreateJsonContent(new LoginRequest(email, password)));
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{body}{Environment.NewLine}{GetIntegrationLogs()}");

        return ExtractString(body, "data", "accessToken");
    }

    protected static async Task<int> GetProductStockAsync(HttpClient client, Guid productId)
    {
        var response = await client.GetAsync($"/api/v1/products/{productId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return ExtractInt(body, "data", "stock");
    }

    protected static string ExtractString(string json, params object[] path)
    {
        return ExtractElement(json, path).GetString() ?? string.Empty;
    }

    protected static Guid ExtractGuid(string json, params object[] path)
    {
        return ExtractElement(json, path).GetGuid();
    }

    protected static int ExtractInt(string json, params object[] path)
    {
        return ExtractElement(json, path).GetInt32();
    }

    protected static bool ExtractBool(string json, params object[] path)
    {
        return ExtractElement(json, path).GetBoolean();
    }

    protected static int ExtractArrayLength(string json, params object[] path)
    {
        return ExtractElement(json, path).GetArrayLength();
    }

    protected static JsonElement ExtractElement(string json, params object[] path)
    {
        using var document = JsonDocument.Parse(json);
        var element = document.RootElement;

        foreach (var segment in path)
        {
            element = segment switch
            {
                string propertyName => element.GetProperty(propertyName),
                int index => element[index],
                _ => throw new InvalidOperationException($"Unsupported JSON path segment type {segment.GetType().Name}.")
            };
        }

        return element.Clone();
    }

    protected static void ClearIntegrationLogs()
    {
        lock (IntegrationLogsLock)
        {
            IntegrationLogs.Clear();
        }
    }

    protected static string GetIntegrationLogs()
    {
        lock (IntegrationLogsLock)
        {
            return string.Join(Environment.NewLine, IntegrationLogs);
        }
    }

    protected static string FindRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. pathParts]);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Repository file was not found.", Path.Combine(pathParts));
    }

    protected static User CreateTokenUser(string roleName)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = $"{roleName}@example.com",
            FullName = $"{roleName} User",
            IsActive = true
        };
    }

    protected static string CreateManualJwt(JwtOptions options, DateTime expiresAtUtc)
    {
        var user = CreateTokenUser(SystemRoleNames.Admin);
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey!)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, SystemRoleNames.Admin)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: expiresAtUtc,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected static JwtOptions CloneJwtOptions(
        JwtOptions source,
        string? issuer = null,
        string? audience = null,
        string? signingKey = null)
    {
        return new JwtOptions
        {
            Issuer = issuer ?? source.Issuer,
            Audience = audience ?? source.Audience,
            SigningKey = signingKey ?? source.SigningKey,
            ExpirationMinutes = source.ExpirationMinutes
        };
    }

    public static TheoryData<Exception, int> ExceptionStatusCases => new()
    {
        { new NotFoundException("Missing."), StatusCodes.Status404NotFound },
        { new ConflictException("Conflict."), StatusCodes.Status409Conflict },
        { new ForbiddenException("Forbidden."), StatusCodes.Status403Forbidden },
        { new UnauthorizedException("Unauthorized."), StatusCodes.Status401Unauthorized },
        { new BusinessRuleException("Invalid operation."), StatusCodes.Status422UnprocessableEntity },
        { new ExternalServiceException("External unavailable."), StatusCodes.Status503ServiceUnavailable },
        { new InvalidOperationException("Sensitive detail."), StatusCodes.Status500InternalServerError }
    };
}
