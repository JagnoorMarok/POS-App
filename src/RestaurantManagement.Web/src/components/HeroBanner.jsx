import React from 'react';
import { MapPin, Phone, Clock } from 'lucide-react';

export default function HeroBanner({ restaurant }) {
  return (
    <section className="hero-section">
      <h1 className="hero-title">{restaurant.restaurantName || 'Restaurant Menu'}</h1>
      <p className="hero-subtitle">
        Authentic, freshly crafted dishes made with premium ingredients and synced directly with our restaurant POS.
      </p>

      <div className="restaurant-meta-bar">
        <span className="meta-item">
          <MapPin size={16} color="#f59e0b" />
          <span>{restaurant.address || 'Dine-in & Takeaway'}</span>
        </span>

        {restaurant.phoneNumber && (
          <span className="meta-item">
            <Phone size={16} color="#f59e0b" />
            <span>{restaurant.phoneNumber}</span>
          </span>
        )}

        <span className="meta-item">
          <Clock size={16} color="#f59e0b" />
          <span>Open Daily: 10:00 AM – 11:00 PM</span>
        </span>
      </div>
    </section>
  );
}
