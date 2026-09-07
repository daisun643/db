<template>
  <nav class="tabs" aria-label="交易">
    <router-link
      v-for="tab in tabs"
      :key="tab.to"
      :to="tab.to"
      class="tab"
      :class="{ active: route.path === tab.to }"
    >{{ tab.label }}</router-link>
  </nav>
</template>

<script setup>
import { useRoute } from 'vue-router'

const route = useRoute()

const tabs = [
  { to: '/products', label: '商品列表' },
  { to: '/products/my', label: '我的商品' },
  { to: '/products/orders', label: '我的订单' },
]
</script>

<style scoped>
.tabs {
  display: flex;
  flex-wrap: nowrap;
  gap: .35rem;
}

.tab {
  border: 0;
  border-radius: 12px;
  padding: .72rem 1.05rem;
  color: #70778a;
  background: transparent;
  font-weight: 700;
  cursor: pointer;
  white-space: nowrap;
  text-decoration: none;
}

.tab.active {
  color: #fff;
  background: linear-gradient(135deg, #5f50dc, #7967f3);
  box-shadow: 0 8px 20px rgba(95, 80, 220, .22);
}
</style>
