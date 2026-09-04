<template>
  <div class="page-container product-page">
    <div class="market-nav">
      <ProductTabs v-model="activeTab" />
      <span class="market-nav-note">让闲置更有价值</span>
    </div>

    <MessagePopup :message="error || ''" type="error" @close="error = null" />
    <MessagePopup :message="notice" type="success" @close="notice = ''" />

    <AllProductsPanel
      v-if="activeTab === 'all'"
      :ref="setAllProductsPanel"
      @open-detail="openProductDetail"
      @order="handleOrder"
      @report="openReport"
      @products-changed="myProductsPanel?.reloadProducts()"
      @error="error = $event"
    />

    <MyProductsPanel
      v-else-if="activeTab === 'my-products'"
      :ref="setMyProductsPanel"
      @edit="openEditProduct"
      @dispute="openDispute"
      @messages="openOrderMessages"
      @products-changed="allProductsPanel?.reload()"
      @error="error = $event"
    />

    <OrdersPanel
      v-else-if="activeTab === 'orders'"
      :ref="setOrdersPanel"
      @pay="handlePay"
      @confirm="handleConfirm"
      @cancel="handleCancel"
      @dispute="openDispute"
      @messages="openOrderMessages"
      @error="error = $event"
    />

    <DisputeForm
      :open="!!disputeTarget"
      :order="disputeTarget"
      v-model:reason="disputeReason"
      @close="closeDispute"
      @submit="handleCreateDispute"
    />

    <OrderMessageBoard
      :order="messageTarget"
      :messages="orderMessages"
      v-model:text="orderMessageText"
      @send="handleSendOrderMessage"
    />

    <ReportModal
      :open="!!reportTarget"
      :target="reportTarget"
      v-model:reason="reportReason"
      @close="closeReport"
      @submit="handleCreateReport"
    />

    <ProductDetailDrawer
      :open="detailOpen"
      :product="selectedProduct"
      :loading="detailLoading"
      @close="closeProductDetail"
      @order="handleOrder"
      @report="openReport"
    />

    <ProductEditDrawer
      :product="editingProduct"
      @close="closeEditProduct"
      @saved="handleProductSaved"
      @error="error = $event"
    />
  </div>
</template>

<script setup>
import { ref } from 'vue'
import MessagePopup from '../../components/MessagePopup.vue'
import ReportModal from '../../components/ReportModal.vue'
import ProductTabs from '../../components/market/ProductTabs.vue'
import AllProductsPanel from '../../components/market/AllProductsPanel.vue'
import MyProductsPanel from '../../components/market/MyProductsPanel.vue'
import OrdersPanel from '../../components/market/OrdersPanel.vue'
import ProductDetailDrawer from '../../components/market/ProductDetailDrawer.vue'
import ProductEditDrawer from '../../components/market/ProductEditDrawer.vue'
import DisputeForm from '../../components/market/DisputeForm.vue'
import OrderMessageBoard from '../../components/market/OrderMessageBoard.vue'
import { useMarket } from '../../composables/useMarket'

const {
  error,
  notice,
  registerPanel,
  // 详情
  detailLoading,
  selectedProduct,
  openProductDetail,
  closeProductDetail,
  // 编辑
  editingProduct,
  openEditProduct,
  closeEditProduct,
  handleProductSaved,
  // 下单 / 支付 / 收货 / 取消
  handleCreateOrder,
  handlePay,
  handleConfirm,
  handleCancel,
  // 纠纷
  disputeTarget,
  disputeReason,
  openDispute,
  closeDispute,
  handleCreateDispute,
  // 举报
  reportTarget,
  reportReason,
  openReport,
  closeReport,
  handleCreateReport,
  // 留言板
  messageTarget,
  orderMessages,
  orderMessageText,
  openOrderMessages,
  handleSendOrderMessage,
} = useMarket()

const activeTab = ref('all')

// 各面板挂载时注册刷新句柄（v-if 互斥切换时自动注销），供 useMarket 操作后统一刷新
const allProductsPanel = ref(null)
const myProductsPanel = ref(null)
const ordersPanel = ref(null)
let unregisterAllPanel = null
let unregisterMyPanel = null
let unregisterOrdersPanel = null

const setAllProductsPanel = (el) => {
  allProductsPanel.value = el
  unregisterAllPanel?.()
  unregisterAllPanel = el ? registerPanel({ reloadProducts: () => el.reload() }) : null
}

const setMyProductsPanel = (el) => {
  myProductsPanel.value = el
  unregisterMyPanel?.()
  unregisterMyPanel = el ? registerPanel({
    reloadProducts: () => el.reloadProducts(),
    reloadSales: () => el.reloadSales(),
  }) : null
}

const setOrdersPanel = (el) => {
  ordersPanel.value = el
  unregisterOrdersPanel?.()
  unregisterOrdersPanel = el ? registerPanel({
    reloadOrders: () => el.reload(),
    resetOrders: () => el.resetAndReload(),
  }) : null
}

const openOrderListTab = () => {
  if (activeTab.value === 'orders') {
    ordersPanel.value?.resetAndReload()
    return
  }
  activeTab.value = 'orders'
}

const handleOrder = async (product) => {
  const created = await handleCreateOrder(product)
  if (created) {
    openOrderListTab()
  }
}
</script>

<style scoped>
.product-page {
  --market-ink: #11182b;
  --market-purple: #6654ee;
  --market-teal: #16a6a3;
  width: 100%;
  max-width: 1480px;
  margin: 0 auto;
  color: var(--market-ink);
}

.market-nav { display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin: 1.25rem 0; padding: .55rem; border: 1px solid rgba(24,32,55,.08); border-radius: 18px; background: #fff; box-shadow: 0 10px 30px rgba(28,34,64,.05); }
.market-nav-note { padding-right: .8rem; color: #9a9fb0; font-size: .72rem; }

@media (max-width: 640px) {
  .market-nav { align-items: stretch; overflow-x: auto; }
  .market-nav-note { display: none; }
}
</style>
