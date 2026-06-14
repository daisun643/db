<template>
  <div class="page-container">
    <div class="page-header page-header-tabs">
      <h1 class="page-title">交易</h1>

      <div class="tabs">
        <button 
          :class="['tab', { active: activeTab === 'all' }]" 
          @click="activeTab = 'all'"
        >
          商品列表
        </button>
        <button 
          :class="['tab', { active: activeTab === 'my-products' }]" 
          @click="activeTab = 'my-products'"
        >
          我的商品
        </button>
        <button 
          :class="['tab', { active: activeTab === 'orders' }]" 
          @click="activeTab = 'orders'"
        >
          我的订单
        </button>
      </div>
    </div>

    <div v-if="error" class="error-message">{{ error }}</div>
    <div v-if="loading" class="loading">加载中...</div>

    <div v-else-if="activeTab === 'all'" class="grid">
      <div class="card" v-for="product in products" :key="product.productID">
        <h3 style="margin-bottom: 0.5rem;">{{ product.title }}</h3>
        <p style="color: var(--text-secondary); font-size: 0.875rem; margin-bottom: 0.5rem;">
          {{ product.description || '暂无描述' }}
        </p>
        <div style="display: flex; justify-content: space-between; align-items: center;">
          <span style="font-size: 1.25rem; font-weight: 700; color: var(--primary);">
            ¥{{ product.price }}
          </span>
          <span style="font-size: 0.875rem; color: var(--text-secondary);">
            库存: {{ product.stock }}
          </span>
        </div>
      </div>
      <div v-if="products.length === 0" class="card" style="text-align: center; color: var(--text-secondary);">
        暂无商品
      </div>
    </div>

    <div v-else-if="activeTab === 'my-products'" class="tab-content">
      <div class="empty-state">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="9" cy="21" r="1" />
          <circle cx="20" cy="21" r="1" />
          <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6" />
        </svg>
        <p>暂无发布的商品</p>
      </div>
    </div>

    <div v-else-if="activeTab === 'orders'" class="tab-content">
      <div class="empty-state">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M9 11l3 3L22 4" />
          <path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
        </svg>
        <p>暂无订单</p>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { getProducts } from '../api'

const activeTab = ref('all')
const products = ref([])
const loading = ref(true)
const error = ref(null)

onMounted(async () => {
  try {
    const res = await getProducts()
    products.value = res.data
  } catch (e) {
    error.value = '无法加载商品数据: ' + (e.response?.data?.message || e.message)
  } finally {
    loading.value = false
  }
})
</script>
