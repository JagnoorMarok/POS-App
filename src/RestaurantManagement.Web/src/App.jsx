import React, { useState, useEffect, useMemo } from 'react';
import WelcomeScreen from './components/WelcomeScreen';
import CategoryNavbar from './components/CategoryNavbar';
import ProductListItem from './components/ProductListItem';
import CheckoutDrawer from './components/CheckoutDrawer';
import ActiveOrderStatus from './components/ActiveOrderStatus';
import { Search, Sparkles, Leaf, Drumstick, ShoppingBag, ArrowLeft, Armchair } from 'lucide-react';
import { API_BASE } from './config';
import './App.css';

export default function App() {
  // Navigation View: 'welcome' | 'menu'
  const [currentView, setCurrentView] = useState('welcome');

  const [restaurant, setRestaurant] = useState({
    restaurantName: 'The Grand Restaurant',
    currencySymbol: '₹',
    defaultTaxRatePercent: 5.0
  });

  const [tables, setTables] = useState([]);
  const [selectedTable, setSelectedTable] = useState(null);

  const [categories, setCategories] = useState([]);
  const [products, setProducts] = useState([]);
  const [selectedCategory, setSelectedCategory] = useState('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [dietFilter, setDietFilter] = useState('all');

  const [cart, setCart] = useState([]);
  const [checkoutOpen, setCheckoutOpen] = useState(false);
  const [isPlacingOrder, setIsPlacingOrder] = useState(false);

  const [activeOrder, setActiveOrder] = useState(null);
  const [toasts, setToasts] = useState([]);

  // Toast Helper
  const addToast = (message) => {
    const id = Date.now() + Math.random();
    setToasts((prev) => [...prev, { id, message }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, 3000);
  };

  // Fetch Menu and Tables
  useEffect(() => {
    const loadData = async () => {
      try {
        const menuRes = await fetch(`${API_BASE}/api/menu`);
        if (menuRes.ok) {
          const menuData = await menuRes.json();
          if (menuData.restaurant) setRestaurant(menuData.restaurant);
          if (menuData.categories) {
            setCategories(menuData.categories);
            const flat = [];
            menuData.categories.forEach((cat) => {
              if (cat.products) flat.push(...cat.products);
            });
            setProducts(flat);
          }
        }

        const tableRes = await fetch(`${API_BASE}/api/tables`);
        if (tableRes.ok) {
          const tableList = await tableRes.json();
          setTables(tableList);

          // Check if table query param exists in URL
          const params = new URLSearchParams(window.location.search);
          const tableParam = params.get('table');
          if (tableParam && tableList.length > 0) {
            const match = tableList.find(
              (t) =>
                t.tableNumber?.toLowerCase() === tableParam.toLowerCase() ||
                t.tableNumber?.toLowerCase() === `table ${tableParam}`.toLowerCase() ||
                t.tableNumber?.toLowerCase() === `t-${tableParam}`.toLowerCase()
            );
            if (match) setSelectedTable(match);
          }
        }
      } catch (err) {
        console.error('Error fetching data:', err);
      }
    };

    loadData();
  }, []);

  // Poll Active Order for selected table
  useEffect(() => {
    if (!selectedTable) return;

    const fetchActive = async () => {
      try {
        const res = await fetch(`${API_BASE}/api/tables/${selectedTable.id}/active-order`);
        if (res.ok) {
          const order = await res.json();
          setActiveOrder(order);
        }
      } catch (err) {
        console.error(err);
      }
    };

    fetchActive();
    const interval = setInterval(fetchActive, 4000);
    return () => clearInterval(interval);
  }, [selectedTable]);

  // Cart operations
  const handleAddToCart = (product) => {
    setCart((prev) => {
      const existing = prev.find((i) => i.id === product.id);
      if (existing) {
        return prev.map((i) => (i.id === product.id ? { ...i, quantity: i.quantity + 1 } : i));
      }
      return [...prev, { ...product, quantity: 1, notes: '' }];
    });
    addToast(`Added "${product.name}" to cart`);
  };

  const handleUpdateQuantity = (productId, newQty) => {
    if (newQty <= 0) {
      setCart((prev) => prev.filter((i) => i.id !== productId));
    } else {
      setCart((prev) =>
        prev.map((i) => (i.id === productId ? { ...i, quantity: newQty } : i))
      );
    }
  };

  const handleUpdateNotes = (productId, notes) => {
    setCart((prev) =>
      prev.map((i) => (i.id === productId ? { ...i, notes } : i))
    );
  };

  const handleRemoveFromCart = (productId) => {
    setCart((prev) => prev.filter((i) => i.id !== productId));
  };

  // Place Order on Checkout
  const handleConfirmOrder = async ({ tableIdentifier, notes }) => {
    if (cart.length === 0) return;
    setIsPlacingOrder(true);

    try {
      const payload = {
        tableIdentifier,
        items: cart.map((i) => ({
          productId: i.id,
          quantity: i.quantity,
          notes: i.notes || null
        })),
        notes
      };

      const res = await fetch(`${API_BASE}/api/orders/place`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const err = await res.json();
        throw new Error(err.error || 'Failed to place order');
      }

      const placed = await res.json();
      setActiveOrder(placed);

      // Match table if not set
      if (!selectedTable || selectedTable.id !== placed.tableId) {
        const foundTable = tables.find((t) => t.id === placed.tableId);
        if (foundTable) setSelectedTable(foundTable);
      }

      setCart([]);
      setCheckoutOpen(false);
      addToast(`Order #${placed.orderNumber} sent to kitchen!`);
    } catch (err) {
      console.error(err);
      alert(err.message || 'Error sending order to kitchen.');
    } finally {
      setIsPlacingOrder(false);
    }
  };

  // Filtered menu
  const filteredProducts = useMemo(() => {
    return products.filter((p) => {
      if (selectedCategory !== 'all' && p.categoryId?.toLowerCase() !== selectedCategory.toLowerCase()) {
        return false;
      }
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        const mName = p.name?.toLowerCase().includes(q);
        const mDesc = p.description?.toLowerCase().includes(q);
        if (!mName && !mDesc) return false;
      }
      if (dietFilter === 'veg') {
        const isVeg = p.name?.toLowerCase().includes('veg') && !p.name?.toLowerCase().includes('non-veg');
        const descVeg = p.description?.toLowerCase().includes('vegetarian');
        if (!isVeg && !descVeg) return false;
      } else if (dietFilter === 'nonveg') {
        const isNonVeg =
          p.name?.toLowerCase().includes('chicken') ||
          p.name?.toLowerCase().includes('mutton') ||
          p.name?.toLowerCase().includes('fish') ||
          p.name?.toLowerCase().includes('egg') ||
          p.name?.toLowerCase().includes('non-veg');
        if (!isNonVeg) return false;
      }
      return true;
    });
  }, [products, selectedCategory, searchQuery, dietFilter]);

  const groupedCategories = useMemo(() => {
    return categories
      .map((cat) => {
        const items = filteredProducts.filter(
          (p) => p.categoryId?.toLowerCase() === cat.id?.toLowerCase()
        );
        return { ...cat, items };
      })
      .filter((cat) => cat.items.length > 0);
  }, [categories, filteredProducts]);

  const totalCartCount = cart.reduce((sum, i) => sum + i.quantity, 0);
  const totalCartPrice = cart.reduce((sum, i) => sum + i.price * i.quantity, 0);

  return (
    <div className="min-app">
      {/* 1. Welcome Greeting Screen */}
      {currentView === 'welcome' && (
        <WelcomeScreen
          restaurant={restaurant}
          onExploreMenu={() => setCurrentView('menu')}
        />
      )}

      {/* 2. Menu Screen */}
      {currentView === 'menu' && (
        <>
          {/* Top Brand & Return Header */}
          <header className="menu-top-header">
            <div className="menu-header-inner">
              <div className="menu-brand" onClick={() => setCurrentView('welcome')}>
                <ArrowLeft size={18} color="var(--text-secondary)" />
                <h2>{restaurant.restaurantName}</h2>
              </div>

              {selectedTable && (
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', background: 'var(--bg-elevated)', padding: '0.3rem 0.75rem', borderRadius: 'var(--radius-full)', fontSize: '0.82rem', fontWeight: 600 }}>
                  <Armchair size={14} color="#f59e0b" />
                  <span>Table {selectedTable.tableNumber}</span>
                </div>
              )}
            </div>
          </header>

          {/* Sticky Category Navbar */}
          <CategoryNavbar
            categories={categories}
            selectedCategory={selectedCategory}
            onSelectCategory={setSelectedCategory}
          />

          {/* Main Menu Content */}
          <main className="menu-main-container">
            {/* Live Active Table Order Status (if any) */}
            {activeOrder && (
              <ActiveOrderStatus
                activeOrder={activeOrder}
                currencySymbol={restaurant.currencySymbol}
                onOrderMore={() => {}}
              />
            )}

            {/* Search & Dietary Filters */}
            <div className="search-filter-row">
              <div className="min-search-box">
                <Search size={16} className="min-search-icon" />
                <input
                  type="text"
                  className="min-search-input"
                  placeholder="Search dishes or ingredients..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                />
              </div>

              <div className="diet-filter-group">
                <button
                  type="button"
                  className={`diet-btn ${dietFilter === 'all' ? 'active' : ''}`}
                  onClick={() => setDietFilter('all')}
                >
                  <Sparkles size={13} />
                  <span>All</span>
                </button>
                <button
                  type="button"
                  className={`diet-btn ${dietFilter === 'veg' ? 'active' : ''}`}
                  onClick={() => setDietFilter('veg')}
                >
                  <Leaf size={13} color="#10b981" />
                  <span>Veg</span>
                </button>
                <button
                  type="button"
                  className={`diet-btn ${dietFilter === 'nonveg' ? 'active' : ''}`}
                  onClick={() => setDietFilter('nonveg')}
                >
                  <Drumstick size={13} color="#ef4444" />
                  <span>Non-Veg</span>
                </button>
              </div>
            </div>

            {/* Categorized Food Items List */}
            {groupedCategories.length === 0 ? (
              <div style={{ textAlign: 'center', padding: '3rem 1rem', color: 'var(--text-muted)' }}>
                <p>No dishes found matching your criteria.</p>
              </div>
            ) : (
              groupedCategories.map((group) => (
                <section key={group.id} className="menu-category-section">
                  <div className="category-heading">
                    <span>{group.name}</span>
                    <span style={{ fontSize: '0.8rem', fontWeight: 500, color: 'var(--text-muted)' }}>
                      {group.items.length} {group.items.length === 1 ? 'dish' : 'dishes'}
                    </span>
                  </div>

                  <div className="item-list">
                    {group.items.map((item) => {
                      const cartItem = cart.find((i) => i.id === item.id);
                      return (
                        <ProductListItem
                          key={item.id}
                          product={item}
                          currencySymbol={restaurant.currencySymbol}
                          cartQuantity={cartItem ? cartItem.quantity : 0}
                          onAddToCart={handleAddToCart}
                          onUpdateQuantity={handleUpdateQuantity}
                        />
                      );
                    })}
                  </div>
                </section>
              ))
            )}
          </main>

          {/* Floating Cart Button */}
          {totalCartCount > 0 && !checkoutOpen && (
            <div className="floating-cart-bar">
              <button
                type="button"
                className="cart-bar-btn"
                onClick={() => setCheckoutOpen(true)}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                  <ShoppingBag size={18} />
                  <span>{totalCartCount} {totalCartCount === 1 ? 'Item' : 'Items'}</span>
                </div>
                <span>View Order • {restaurant.currencySymbol}{totalCartPrice.toFixed(2)}</span>
              </button>
            </div>
          )}

          {/* Checkout & Table Prompt Drawer */}
          <CheckoutDrawer
            isOpen={checkoutOpen}
            onClose={() => setCheckoutOpen(false)}
            cart={cart}
            tables={tables}
            selectedTable={selectedTable}
            onSelectTable={setSelectedTable}
            currencySymbol={restaurant.currencySymbol}
            taxRatePercent={restaurant.defaultTaxRatePercent}
            onUpdateQuantity={handleUpdateQuantity}
            onUpdateNotes={handleUpdateNotes}
            onRemoveItem={handleRemoveFromCart}
            onConfirmOrder={handleConfirmOrder}
            isPlacingOrder={isPlacingOrder}
          />
        </>
      )}

      {/* Toast Messages */}
      {toasts.length > 0 && (
        <div className="toast-container">
          {toasts.map((t) => (
            <div key={t.id} className="toast-msg">
              <span>{t.message}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
