<template>
  <AllProductsPanel
    :ref="setPanel"
    @open-detail="openProductDetail"
    @order="handleOrder"
    @report="openReport"
    @error="error = $event"
  />
</template>

<script setup>
import { useRouter } from 'vue-router'
import AllProductsPanel from '../../components/market/AllProductsPanel.vue'
import { useMarket } from '../../composables/useMarket'

const { error, registerPanel, openProductDetail, openReport, handleCreateOrder } = useMarket()

const router = useRouter()

// 面板挂载时注册刷新句柄（离开页面时自动注销），供 useMarket 操作后统一刷新
let unregisterPanel = null
const setPanel = (el) => {
  unregisterPanel?.()
  unregisterPanel = el ? registerPanel({ reloadProducts: () => el.reload() }) : null
}

// 下单成功后跳到我的订单页
const handleOrder = async (product) => {
  if (await handleCreateOrder(product)) {
    router.push('/products/orders')
  }
}
</script>
