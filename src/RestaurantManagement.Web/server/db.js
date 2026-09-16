import { DatabaseSync } from 'node:sqlite';
import path from 'node:path';
import crypto from 'node:crypto';

// Resolve database path in %LOCALAPPDATA%/RestaurantManagement/Data/restaurant.db
const localAppData = process.env.LOCALAPPDATA || path.join(process.env.USERPROFILE || 'C:\\Users\\Dell', 'AppData', 'Local');
const dbPath = path.join(localAppData, 'RestaurantManagement', 'Data', 'restaurant.db');

let dbInstance = null;

export function getDb() {
  if (!dbInstance) {
    dbInstance = new DatabaseSync(dbPath);
    // Ensure WAL mode for concurrent desktop + web operation
    dbInstance.exec('PRAGMA journal_mode = WAL;');
  }
  return dbInstance;
}

/**
 * Get Restaurant Profile & default tax configuration
 */
export function getRestaurantProfile() {
  const db = getDb();
  const profileRow = db.prepare('SELECT * FROM RestaurantProfiles LIMIT 1').get() || {};
  const taxRow = db.prepare("SELECT Value FROM ApplicationSettings WHERE Key = 'app_default_tax_rate' LIMIT 1").get();
  const currencyRow = db.prepare("SELECT Value FROM ApplicationSettings WHERE Key = 'app_currency_symbol' LIMIT 1").get();

  return {
    id: profileRow.Id || null,
    restaurantName: profileRow.RestaurantName || 'Restaurant POS Menu',
    address: profileRow.Address || 'Dine-in & Takeaway',
    phoneNumber: profileRow.PhoneNumber || '',
    currencySymbol: currencyRow?.Value || profileRow.CurrencySymbol || '₹',
    defaultTaxRatePercent: taxRow ? parseFloat(taxRow.Value) : 5.0
  };
}

/**
 * Get all active restaurant tables
 */
export function getTables() {
  const db = getDb();
  const sql = `
    SELECT 
      Id as id,
      TableNumber as tableNumber,
      Capacity as capacity,
      IsActive as isActive,
      DisplayOrder as displayOrder
    FROM RestaurantTables
    WHERE IsActive = 1
    ORDER BY DisplayOrder ASC, TableNumber ASC;
  `;
  return db.prepare(sql).all();
}

/**
 * Get single table by ID or TableNumber
 */
export function getTableByIdOrNumber(identifier) {
  const db = getDb();
  const normalized = String(identifier).trim();
  let table = db.prepare('SELECT Id as id, TableNumber as tableNumber, Capacity as capacity, IsActive as isActive FROM RestaurantTables WHERE Id = ?').get(normalized.toUpperCase());
  if (!table) {
    table = db.prepare('SELECT Id as id, TableNumber as tableNumber, Capacity as capacity, IsActive as isActive FROM RestaurantTables WHERE UPPER(TableNumber) = ?').get(normalized.toUpperCase());
  }
  return table;
}

/**
 * Get all active categories with product counts
 */
export function getCategories(includeInactive = false) {
  const db = getDb();
  const sql = `
    SELECT 
      c.Id as id,
      c.Name as name,
      c.Description as description,
      c.DisplayOrder as displayOrder,
      c.IsActive as isActive,
      (SELECT COUNT(*) FROM Products p WHERE p.CategoryId = c.Id AND (${includeInactive ? '1=1' : 'p.IsActive = 1'})) as productCount,
      c.CreatedAt as createdAt,
      c.UpdatedAt as updatedAt
    FROM Categories c
    ${includeInactive ? '' : 'WHERE c.IsActive = 1'}
    ORDER BY c.DisplayOrder ASC, c.Name ASC;
  `;
  return db.prepare(sql).all();
}

/**
 * Get all products
 */
export function getProducts(includeInactive = false, categoryId = null) {
  const db = getDb();
  let sql = `
    SELECT 
      p.Id as id,
      p.Name as name,
      p.Description as description,
      p.Price as price,
      p.CategoryId as categoryId,
      c.Name as categoryName,
      p.IsActive as isActive,
      p.IsAvailable as isAvailable,
      p.DisplayOrder as displayOrder,
      p.ImagePath as imagePath,
      p.CreatedAt as createdAt,
      p.UpdatedAt as updatedAt
    FROM Products p
    INNER JOIN Categories c ON p.CategoryId = c.Id
    WHERE 1=1
  `;
  const params = [];

  if (!includeInactive) {
    sql += ' AND p.IsActive = 1 AND c.IsActive = 1';
  }
  if (categoryId) {
    sql += ' AND p.CategoryId = ?';
    params.push(categoryId.toUpperCase());
  }

  sql += ' ORDER BY c.DisplayOrder ASC, p.DisplayOrder ASC, p.Name ASC;';
  return db.prepare(sql).all(...params);
}

/**
 * Get full customer menu (Restaurant profile + active categories & available products)
 */
export function getFullMenu() {
  const profile = getRestaurantProfile();
  const categories = getCategories(false);
  const products = getProducts(false);

  const categoriesWithProducts = categories.map(cat => {
    const catProducts = products
      .filter(p => p.categoryId?.toLowerCase() === cat.id?.toLowerCase())
      .map(p => ({
        ...p,
        isAvailable: Boolean(p.isAvailable),
        isActive: Boolean(p.isActive),
        stockStatus: p.isAvailable ? 'InStock' : 'OutOfStock'
      }));

    return {
      ...cat,
      isActive: Boolean(cat.isActive),
      productCount: catProducts.length,
      products: catProducts
    };
  });

  return {
    restaurant: profile,
    categories: categoriesWithProducts
  };
}

/**
 * Helper to map OrderStatus integer to readable name
 */
export function mapOrderStatusName(statusInt) {
  switch (statusInt) {
    case 1: return 'Draft';
    case 2: return 'Confirmed';
    case 3: return 'Preparing';
    case 4: return 'Ready';
    case 5: return 'Served';
    case 6: return 'Completed';
    case 7: return 'Cancelled';
    default: return 'Active';
  }
}

/**
 * Get active open order for a specific table
 */
export function getActiveOrderByTableId(tableId) {
  const db = getDb();
  const table = getTableByIdOrNumber(tableId);
  if (!table) return null;

  const orderRow = db.prepare(`
    SELECT 
      Id as id,
      OrderNumber as orderNumber,
      RestaurantTableId as tableId,
      Status as status,
      OrderType as orderType,
      Subtotal as subtotal,
      DiscountAmount as discountAmount,
      TaxAmount as taxAmount,
      TotalAmount as totalAmount,
      Notes as notes,
      CreatedAt as createdAt,
      UpdatedAt as updatedAt
    FROM Orders
    WHERE RestaurantTableId = ? AND Status NOT IN (6, 7)
    ORDER BY CreatedAt DESC
    LIMIT 1;
  `).get(table.id);

  if (!orderRow) return null;

  const items = db.prepare(`
    SELECT 
      Id as id,
      OrderId as orderId,
      ProductId as productId,
      ProductNameSnapshot as name,
      UnitPrice as unitPrice,
      Quantity as quantity,
      DiscountAmount as discountAmount,
      TaxAmount as taxAmount,
      TotalAmount as totalAmount,
      Notes as notes
    FROM OrderItems
    WHERE OrderId = ?;
  `).all(orderRow.id);

  return {
    ...orderRow,
    tableNumber: table.tableNumber,
    statusName: mapOrderStatusName(orderRow.status),
    items
  };
}

/**
 * Place a customer order for a table (or append items to existing active table order)
 */
export function placeCustomerTableOrder({ tableIdentifier, items, notes = null }) {
  const db = getDb();
  const table = getTableByIdOrNumber(tableIdentifier);
  if (!table) {
    throw new Error(`Table '${tableIdentifier}' not found.`);
  }

  if (!items || !Array.isArray(items) || items.length === 0) {
    throw new Error('No order items provided.');
  }

  const profile = getRestaurantProfile();
  const taxRate = profile.defaultTaxRatePercent || 5.0;
  const now = new Date().toISOString();

  // Check if an active open order exists for this table
  let existingOrder = db.prepare(`
    SELECT Id as id, OrderNumber as orderNumber, Notes as notes, Subtotal as subtotal, TaxAmount as taxAmount, TotalAmount as totalAmount
    FROM Orders 
    WHERE RestaurantTableId = ? AND Status NOT IN (6, 7)
    ORDER BY CreatedAt DESC
    LIMIT 1;
  `).get(table.id);

  let orderId = existingOrder ? existingOrder.id : null;
  let orderNumber = existingOrder ? existingOrder.orderNumber : null;

  if (!orderId) {
    // Generate order number ORD-YYYYMMDD-XXXX
    const today = new Date();
    const dateStr = today.toISOString().slice(0, 10).replace(/-/g, '');
    const countRow = db.prepare(`
      SELECT COUNT(*) as count FROM Orders WHERE OrderNumber LIKE ?
    `).get(`ORD-${dateStr}-%`);
    const nextSeq = ((countRow?.count || 0) + 1).toString().padStart(4, '0');
    orderNumber = `ORD-${dateStr}-${nextSeq}`;
    orderId = crypto.randomUUID().toUpperCase();

    // Insert new Order: Status = 2 (Confirmed / In Kitchen), OrderType = 1 (DineIn)
    db.prepare(`
      INSERT INTO Orders (Id, OrderNumber, RestaurantTableId, Status, OrderType, Subtotal, DiscountAmount, TaxAmount, TotalAmount, Notes, CreatedAt, UpdatedAt, CompletedAt)
      VALUES (?, ?, ?, 2, 1, 0, 0, 0, 0, ?, ?, ?, NULL);
    `).run(orderId, orderNumber, table.id, notes || null, now, now);
  } else if (notes) {
    // Append order notes
    const combinedNotes = existingOrder.notes ? `${existingOrder.notes} | ${notes}` : notes;
    db.prepare('UPDATE Orders SET Notes = ?, UpdatedAt = ? WHERE Id = ?').run(combinedNotes, now, orderId);
  }

  // Insert Order Items
  for (const item of items) {
    const product = db.prepare('SELECT Id, Name, Price, IsAvailable FROM Products WHERE Id = ?').get(item.productId.toUpperCase());
    if (!product) continue;

    const unitPrice = parseFloat(product.Price);
    const quantity = parseInt(item.quantity) || 1;
    const itemSubtotal = unitPrice * quantity;
    // Calculate 5% tax
    const itemTax = Math.round(itemSubtotal * (taxRate / 100) * 100) / 100;
    const itemTotal = itemSubtotal + itemTax;
    const itemId = crypto.randomUUID().toUpperCase();

    db.prepare(`
      INSERT INTO OrderItems (Id, OrderId, ProductId, ProductNameSnapshot, UnitPrice, Quantity, DiscountAmount, TaxAmount, TotalAmount, Notes)
      VALUES (?, ?, ?, ?, ?, ?, 0, ?, ?, ?);
    `).run(itemId, orderId, product.Id, product.Name, unitPrice, quantity, itemTax, itemTotal, item.notes || null);
  }

  // Recalculate Order totals from all OrderItems and set Status = 2 (Confirmed / Kitchen)
  const totals = db.prepare(`
    SELECT 
      COALESCE(SUM(UnitPrice * Quantity), 0) as subtotal,
      COALESCE(SUM(TaxAmount), 0) as taxAmount,
      COALESCE(SUM(TotalAmount), 0) as totalAmount
    FROM OrderItems
    WHERE OrderId = ?;
  `).get(orderId);

  db.prepare(`
    UPDATE Orders 
    SET Subtotal = ?, TaxAmount = ?, TotalAmount = ?, Status = 2, UpdatedAt = ?
    WHERE Id = ?;
  `).run(totals.subtotal, totals.taxAmount, totals.totalAmount, now, orderId);

  return getActiveOrderByTableId(table.id);
}
