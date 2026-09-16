import express from 'express';
import cors from 'cors';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import * as db from './db.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = process.env.PORT || 5173;

app.use(cors());
app.use(express.json());

// Serve static frontend in production
const distPath = path.join(__dirname, '..', 'dist');
app.use(express.static(distPath));

// Health Check
app.get('/health', (req, res) => {
  try {
    const profile = db.getRestaurantProfile();
    res.json({
      status: 'Healthy',
      timestamp: new Date().toISOString(),
      restaurant: profile.restaurantName,
      engine: 'React Table Ordering + Node.js + SQLite'
    });
  } catch (err) {
    res.status(503).json({ status: 'Degraded', error: err.message });
  }
});

// Menu Aggregation
app.get('/api/menu', (req, res) => {
  try {
    const menu = db.getFullMenu();
    res.json(menu);
  } catch (err) {
    console.error('Error in /api/menu:', err);
    res.status(500).json({ error: err.message });
  }
});

// Tables List
app.get('/api/tables', (req, res) => {
  try {
    const tables = db.getTables();
    res.json(tables);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Active Order for a Table
app.get('/api/tables/:identifier/active-order', (req, res) => {
  try {
    const activeOrder = db.getActiveOrderByTableId(req.params.identifier);
    res.json(activeOrder || null);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Place Customer Table Order
app.post('/api/orders/place', (req, res) => {
  try {
    const { tableIdentifier, items, notes } = req.body;
    if (!tableIdentifier) {
      return res.status(400).json({ error: 'Table identifier is required' });
    }
    if (!items || items.length === 0) {
      return res.status(400).json({ error: 'At least one item is required' });
    }

    const order = db.placeCustomerTableOrder({
      tableIdentifier,
      items,
      notes
    });

    res.status(201).json(order);
  } catch (err) {
    console.error('Error placing order:', err);
    res.status(400).json({ error: err.message });
  }
});

// Categories Endpoints
app.get('/api/categories', (req, res) => {
  try {
    const categories = db.getCategories(false);
    res.json(categories);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Products Endpoints
app.get('/api/products', (req, res) => {
  try {
    const products = db.getProducts(false, req.query.categoryId);
    res.json(products);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// Fallback to React index.html in production
app.get('*', (req, res) => {
  res.sendFile(path.join(distPath, 'index.html'));
});

app.listen(PORT, () => {
  console.log(`[Customer Table Ordering Server] Running on http://localhost:${PORT}`);
});
