<template>
  <div class="page-container product-page">
    <div class="market-nav">
      <ProductTabs />
      <span class="market-nav-note">让闲置更有价值</span>
    </div>

    <MessagePopup :message="error || ''" type="error" @close="error = null" />
    <MessagePopup :message="notice" type="success" @close="notice = ''" />

    <!-- 商品列表 / 我的商品 / 我的订单 三个子页面，对应 router 中 /products 的 children -->
    <router-view />

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
      v-model:description="reportDescription"
      @close="closeReport"
      @submit="handleCreateReport"
    />

    <ProductDetailDrawer
      :open="!!selectedProduct"
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
import { useRouter } from 'vue-router'
import MessagePopup from '../../components/MessagePopup.vue'
import ReportModal from '../../components/ReportModal.vue'
import ProductTabs from '../../components/market/ProductTabs.vue'
import ProductDetailDrawer from '../../components/market/ProductDetailDrawer.vue'
import ProductEditDrawer from '../../components/market/ProductEditDrawer.vue'
import DisputeForm from '../../components/market/DisputeForm.vue'
import OrderMessageBoard from '../../components/market/OrderMessageBoard.vue'
import { useMarket } from '../../composables/useMarket'

const {
  error,
  notice,
  // 详情
  detailLoading,
  selectedProduct,
  closeProductDetail,
  // 编辑
  editingProduct,
  closeEditProduct,
  handleProductSaved,
  // 下单（详情抽屉内下单）
  handleCreateOrder,
  // 纠纷
  disputeTarget,
  disputeReason,
  closeDispute,
  handleCreateDispute,
  // 举报
  reportTarget,
  reportReason,
  reportDescription,
  openReport,
  closeReport,
  handleCreateReport,
  // 留言板
  messageTarget,
  orderMessages,
  orderMessageText,
  handleSendOrderMessage,
} = useMarket()

const router = useRouter()

// 下单成功后跳到我的订单页（商品列表面板内的下单在 AllProductsView 中做同样跳转）
const handleOrder = async (product) => {
  if (await handleCreateOrder(product)) {
    router.push('/products/orders')
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
