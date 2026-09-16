/**
 * Restaurant Management — Web Menu Application
 * Communicates with ASP.NET Core REST API & SQLite Database
 */

// State
const state = {
  restaurant: {
    name: 'Restaurant Menu',
    tagline: 'Fresh & Delicious Everyday',
    currency: '₹',
    address: '',
    phone: '',
    taxRate: 5.0
  },
  categories: [],
  products: [],
  selectedCategoryId: 'all',
  searchQuery: '',
  selectedDietFilter: 'all',
  editingProduct: null
};

// DOM Elements
const elements = {
  restaurantNameHeader: document.getElementById('restaurant-name-header'),
  restaurantTaglineHeader: document.getElementById('restaurant-tagline-header'),
  heroTitle: document.getElementById('hero-title'),
  heroSubtitle: document.getElementById('hero-subtitle'),
  heroAddress: document.getElementById('hero-address'),
  heroPhone: document.getElementById('hero-phone'),
  categoriesBar: document.getElementById('categories-bar'),
  menuContainer: document.getElementById('menu-container'),
  searchInput: document.getElementById('search-input'),
  dietFilters: document.querySelectorAll('.filter-btn'),
  itemModal: document.getElementById('item-modal'),
  itemForm: document.getElementById('item-form'),
  modalTitle: document.getElementById('modal-title'),
  modalCloseBtn: document.getElementById('modal-close-btn'),
  modalCancelBtn: document.getElementById('modal-cancel-btn'),
  modalDeleteBtn: document.getElementById('modal-delete-btn'),
  addNewBtn: document.getElementById('btn-add-new-item'),
  toastContainer: document.getElementById('toast-container'),
  // Form fields
  formId: document.getElementById('form-item-id'),
  formName: document.getElementById('form-item-name'),
  formCategory: document.getElementById('form-item-category'),
  formPrice: document.getElementById('form-item-price'),
  formDescription: document.getElementById('form-item-description'),
  formAvailable: document.getElementById('form-item-available'),
  formOrder: document.getElementById('form-item-order')
};

// Initialize Application
document.addEventListener('DOMContentLoaded', () => {
  setupEventListeners();
  loadMenuData();
});

// Event Listeners
function setupEventListeners() {
  // Search
  elements.searchInput?.addEventListener('input', (e) => {
    state.searchQuery = e.target.value.toLowerCase().trim();
    renderMenu();
  });

  // Dietary Filters
  elements.dietFilters.forEach(btn => {
    btn.addEventListener('click', () => {
      elements.dietFilters.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      state.selectedDietFilter = btn.dataset.filter;
      renderMenu();
    });
  });

  // Modal controls
  elements.addNewBtn?.addEventListener('click', () => openCreateModal());
  elements.modalCloseBtn?.addEventListener('click', closeModal);
  elements.modalCancelBtn?.addEventListener('click', closeModal);
  elements.itemModal?.addEventListener('click', (e) => {
    if (e.target === elements.itemModal) closeModal();
  });

  // Form Submission
  elements.itemForm?.addEventListener('submit', handleFormSubmit);
  elements.modalDeleteBtn?.addEventListener('click', handleDeleteProduct);
}

// Fetch Menu Data from API
async function loadMenuData() {
  try {
    const res = await fetch('/api/menu');
    if (!res.ok) throw new Error(`Failed to load menu data (${res.status})`);

    const data = await res.json();

    // Map restaurant profile
    if (data.restaurant) {
      state.restaurant.name = data.restaurant.restaurantName || 'Restaurant Menu';
      state.restaurant.tagline = data.restaurant.tagline || 'Fresh & Delicious Everyday';
      state.restaurant.currency = data.restaurant.currencySymbol || '₹';
      state.restaurant.address = data.restaurant.address || '';
      state.restaurant.phone = data.restaurant.phoneNumber || '';
      state.restaurant.taxRate = data.restaurant.defaultTaxRatePercent || 5.0;
      updateRestaurantHeader();
    }

    // Map categories and flatten products
    state.categories = data.categories || [];
    state.products = [];
    state.categories.forEach(cat => {
      if (cat.products && Array.isArray(cat.products)) {
        state.products.push(...cat.products);
      }
    });

    populateCategoryOptions();
    renderCategoriesBar();
    renderMenu();
  } catch (err) {
    console.error('Error loading menu:', err);
    showToast('Failed to connect to menu database.', 'error');
  }
}

// Update Restaurant Profile in UI
function updateRestaurantHeader() {
  if (elements.restaurantNameHeader) elements.restaurantNameHeader.textContent = state.restaurant.name;
  if (elements.restaurantTaglineHeader) elements.restaurantTaglineHeader.textContent = state.restaurant.tagline;
  if (elements.heroTitle) elements.heroTitle.textContent = state.restaurant.name;
  if (elements.heroSubtitle) elements.heroSubtitle.textContent = state.restaurant.tagline;
  if (elements.heroAddress) elements.heroAddress.textContent = state.restaurant.address ? `📍 ${state.restaurant.address}` : '📍 Dine-in & Takeaway';
  if (elements.heroPhone) elements.heroPhone.textContent = state.restaurant.phone ? `📞 ${state.restaurant.phone}` : '';
}

// Populate Category select in modal
function populateCategoryOptions() {
  if (!elements.formCategory) return;
  elements.formCategory.innerHTML = state.categories.map(c => `
    <option value="${c.id}">${escapeHtml(c.name)}</option>
  `).join('');
}

// Render Category Filter Pills
function renderCategoriesBar() {
  if (!elements.categoriesBar) return;

  const totalCount = state.products.length;

  let html = `
    <button class="category-chip ${state.selectedCategoryId === 'all' ? 'active' : ''}" data-cat-id="all">
      🍽️ All Items
      <span class="category-badge-count">${totalCount}</span>
    </button>
  `;

  state.categories.forEach(cat => {
    const isActive = state.selectedCategoryId === cat.id;
    html += `
      <button class="category-chip ${isActive ? 'active' : ''}" data-cat-id="${cat.id}">
        ${escapeHtml(cat.name)}
        <span class="category-badge-count">${cat.productCount}</span>
      </button>
    `;
  });

  elements.categoriesBar.innerHTML = html;

  elements.categoriesBar.querySelectorAll('.category-chip').forEach(btn => {
    btn.addEventListener('click', () => {
      state.selectedCategoryId = btn.dataset.catId;
      renderCategoriesBar();
      renderMenu();
    });
  });
}

// Filter and Render Menu Items
function renderMenu() {
  if (!elements.menuContainer) return;

  // Filter products
  let filtered = state.products.filter(p => {
    // Category filter
    if (state.selectedCategoryId !== 'all' && p.categoryId !== state.selectedCategoryId) {
      return false;
    }

    // Search query
    if (state.searchQuery) {
      const matchName = p.name.toLowerCase().includes(state.searchQuery);
      const matchDesc = p.description && p.description.toLowerCase().includes(state.searchQuery);
      const matchCat = p.categoryName && p.categoryName.toLowerCase().includes(state.searchQuery);
      if (!matchName && !matchDesc && !matchCat) return false;
    }

    // Dietary filter
    if (state.selectedDietFilter === 'veg') {
      const isVeg = p.name.toLowerCase().includes('veg') && !p.name.toLowerCase().includes('non-veg');
      const descVeg = p.description && p.description.toLowerCase().includes('vegetarian');
      if (!isVeg && !descVeg) return false;
    } else if (state.selectedDietFilter === 'nonveg') {
      const isNonVeg = p.name.toLowerCase().includes('chicken') || 
                         p.name.toLowerCase().includes('mutton') || 
                         p.name.toLowerCase().includes('fish') || 
                         p.name.toLowerCase().includes('egg') ||
                         p.name.toLowerCase().includes('non-veg');
      if (!isNonVeg) return false;
    }

    return true;
  });

  if (filtered.length === 0) {
    elements.menuContainer.innerHTML = `
      <div class="empty-state">
        <div class="empty-icon">🔍</div>
        <h3 class="empty-title">No menu items found</h3>
        <p class="empty-text">Try changing your search query or category filter.</p>
      </div>
    `;
    return;
  }

  // Group filtered items by category
  const grouped = {};
  state.categories.forEach(cat => {
    const items = filtered.filter(p => p.categoryId === cat.id);
    if (items.length > 0) {
      grouped[cat.id] = { category: cat, items };
    }
  });

  let html = '';
  Object.values(grouped).forEach(group => {
    html += `
      <section class="menu-section">
        <div class="section-header">
          <h2 class="section-title">
            <span>${escapeHtml(group.category.name)}</span>
          </h2>
          <span class="section-count">${group.items.length} ${group.items.length === 1 ? 'item' : 'items'}</span>
        </div>
        <div class="product-grid">
          ${group.items.map(item => renderProductCard(item)).join('')}
        </div>
      </section>
    `;
  });

  elements.menuContainer.innerHTML = html;

  // Attach event handlers for cards
  attachCardEvents();
}

// Generate single product card HTML
function renderProductCard(p) {
  const isOutOfStock = !p.isAvailable || p.stockStatus === 'OutOfStock';
  const isLowStock = p.stockStatus === 'LowStock';

  let stockTag = `<span class="status-tag in-stock">● In Stock</span>`;
  if (isOutOfStock) {
    stockTag = `<span class="status-tag out-stock">✕ Out of Stock</span>`;
  } else if (isLowStock) {
    stockTag = `<span class="status-tag low-stock">⚠ Low Stock</span>`;
  }

  const formattedPrice = `${state.restaurant.currency}${Number(p.price).toFixed(2)}`;

  return `
    <div class="product-card ${isOutOfStock ? 'out-of-stock' : ''}" data-product-id="${p.id}">
      <div>
        <div class="card-top">
          <div class="card-title-group">
            <h3 class="card-title">${escapeHtml(p.name)}</h3>
            <div class="card-category">${escapeHtml(p.categoryName || '')}</div>
          </div>
          ${stockTag}
        </div>
        <p class="card-description">${escapeHtml(p.description || 'Freshly prepared with quality ingredients.')}</p>
      </div>

      <div class="card-bottom">
        <div class="card-price">${formattedPrice}</div>
        <div class="card-actions">
          <button class="btn-toggle-stock" data-action="toggle-stock" title="Toggle Stock Availability">
            ${p.isAvailable ? 'Mark Out of Stock' : 'Mark Available'}
          </button>
          <button class="btn-icon" data-action="edit" title="Edit Item Details">
            ✏️
          </button>
        </div>
      </div>
    </div>
  `;
}

// Attach Event Listeners to Product Cards
function attachCardEvents() {
  document.querySelectorAll('.product-card').forEach(card => {
    const id = card.dataset.productId;
    const product = state.products.find(p => p.id === id);
    if (!product) return;

    // Toggle stock button
    card.querySelector('[data-action="toggle-stock"]')?.addEventListener('click', async () => {
      await toggleProductStock(product);
    });

    // Edit button
    card.querySelector('[data-action="edit"]')?.addEventListener('click', () => {
      openEditModal(product);
    });
  });
}

// Toggle Product Stock Availability via PATCH
async function toggleProductStock(product) {
  const newStatus = !product.isAvailable;
  try {
    const res = await fetch(`/api/products/${product.id}/availability`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ isAvailable: newStatus })
    });

    if (!res.ok) throw new Error('Failed to update availability');

    product.isAvailable = newStatus;
    product.stockStatus = newStatus ? 'InStock' : 'OutOfStock';
    showToast(`${product.name} is now ${newStatus ? 'Available' : 'Out of Stock'}`, 'success');
    renderMenu();
  } catch (err) {
    console.error('Error toggling availability:', err);
    showToast('Failed to update product availability.', 'error');
  }
}

// Modal Handlers
function openCreateModal() {
  state.editingProduct = null;
  if (elements.modalTitle) elements.modalTitle.textContent = 'Add New Menu Item';
  if (elements.modalDeleteBtn) elements.modalDeleteBtn.style.display = 'none';

  if (elements.formId) elements.formId.value = '';
  if (elements.formName) elements.formName.value = '';
  if (elements.formPrice) elements.formPrice.value = '';
  if (elements.formDescription) elements.formDescription.value = '';
  if (elements.formAvailable) elements.formAvailable.checked = true;
  if (elements.formOrder) elements.formOrder.value = state.products.length + 1;

  if (elements.formCategory && state.categories.length > 0) {
    elements.formCategory.value = state.selectedCategoryId !== 'all' ? state.selectedCategoryId : state.categories[0].id;
  }

  elements.itemModal?.classList.add('open');
}

function openEditModal(product) {
  state.editingProduct = product;
  if (elements.modalTitle) elements.modalTitle.textContent = 'Edit Menu Item';
  if (elements.modalDeleteBtn) elements.modalDeleteBtn.style.display = 'block';

  if (elements.formId) elements.formId.value = product.id;
  if (elements.formName) elements.formName.value = product.name;
  if (elements.formCategory) elements.formCategory.value = product.categoryId;
  if (elements.formPrice) elements.formPrice.value = product.price;
  if (elements.formDescription) elements.formDescription.value = product.description || '';
  if (elements.formAvailable) elements.formAvailable.checked = product.isAvailable;
  if (elements.formOrder) elements.formOrder.value = product.displayOrder || 0;

  elements.itemModal?.classList.add('open');
}

function closeModal() {
  elements.itemModal?.classList.remove('open');
  state.editingProduct = null;
}

// Save or Update Product
async function handleFormSubmit(e) {
  e.preventDefault();

  const name = elements.formName.value.trim();
  const categoryId = elements.formCategory.value;
  const price = parseFloat(elements.formPrice.value);
  const description = elements.formDescription.value.trim();
  const isAvailable = elements.formAvailable.checked;
  const displayOrder = parseInt(elements.formOrder.value) || 0;

  if (!name || isNaN(price) || price < 0 || !categoryId) {
    showToast('Please provide valid name, price, and category.', 'error');
    return;
  }

  try {
    if (state.editingProduct) {
      // Update
      const payload = {
        id: state.editingProduct.id,
        name,
        price,
        categoryId,
        description: description || null,
        displayOrder,
        isActive: true,
        isAvailable
      };

      const res = await fetch(`/api/products/${state.editingProduct.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!res.ok) throw new Error('Failed to update product');

      showToast(`Updated "${name}" successfully`, 'success');
    } else {
      // Create
      const payload = {
        name,
        price,
        categoryId,
        description: description || null,
        displayOrder,
        isActive: true,
        isAvailable
      };

      const res = await fetch('/api/products', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!res.ok) throw new Error('Failed to create product');

      showToast(`Added "${name}" to menu`, 'success');
    }

    closeModal();
    await loadMenuData(); // Reload menu data from SQLite DB
  } catch (err) {
    console.error('Error saving product:', err);
    showToast('Error saving menu item to database.', 'error');
  }
}

// Delete Product
async function handleDeleteProduct() {
  if (!state.editingProduct) return;
  if (!confirm(`Are you sure you want to delete "${state.editingProduct.name}"?`)) return;

  try {
    const res = await fetch(`/api/products/${state.editingProduct.id}`, {
      method: 'DELETE'
    });

    if (!res.ok) throw new Error('Failed to delete product');

    showToast(`Deleted "${state.editingProduct.name}"`, 'success');
    closeModal();
    await loadMenuData();
  } catch (err) {
    console.error('Error deleting product:', err);
    showToast('Error deleting item from database.', 'error');
  }
}

// Toast Notifications
function showToast(message, type = 'info') {
  if (!elements.toastContainer) return;

  const toast = document.createElement('div');
  toast.className = `toast ${type}`;
  toast.innerHTML = `
    <span>${type === 'success' ? '✓' : type === 'error' ? '✕' : 'ℹ'}</span>
    <span>${escapeHtml(message)}</span>
  `;

  elements.toastContainer.appendChild(toast);

  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateY(10px)';
    toast.style.transition = 'all 0.25s ease';
    setTimeout(() => toast.remove(), 250);
  }, 3500);
}

// Helper: Escape HTML
function escapeHtml(str) {
  if (!str) return '';
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}
