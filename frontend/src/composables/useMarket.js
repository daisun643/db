import { ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  cancelTransaction,
  confirmReceipt,
  createDispute,
  createReport,
  createTransaction,
  getOrderMessages,
  getProduct,
  payTransaction,
  sendOrderMessage,
} from '../api'

// 模块级共享状态：商品页布局与各弹层组件共用同一份数据与弹窗状态
const error = ref(null)
const notice = ref('')

// 商品详情抽屉
const detailLoading = ref(false)
const selectedProduct = ref(null)

// 编辑商品（表单状态留在编辑抽屉组件内）
const editingProduct = ref(null)

// 纠纷 / 举报 / 订单留言板
const disputeTarget = ref(null)
const disputeReason = ref('')
const reportTarget = ref(null)
const reportReason = ref('')
const reportDescription = ref('')
const messageTarget = ref(null)
const orderMessages = ref([])
const orderMessageText = ref('')

// 各面板注册自己的句柄，用于操作后按语义统一刷新（对应 useForum 的 registerFeed）
const panelHandles = new Set()

const registerPanel = (handle) => {
  panelHandles.add(handle)
  return () => panelHandles.delete(handle)
}

const reloadProductFeeds = () => {
  for (const handle of panelHandles) {
    handle.reloadProducts?.()
  }
}

const reloadSalesPanels = () => {
  for (const handle of panelHandles) {
    handle.reloadSales?.()
  }
}

const reloadOrderPanel = () => {
  for (const handle of panelHandles) {
    handle.reloadOrders?.()
  }
}

// ---- 工具 ----

export const statusText = (status) => ({
  Active: '发布中',
  Locked: '已锁定',
  Sold: '已售出',
  Inactive: '已下架',
}[status] || status || '未知')

export const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export const isOrderArchived = (order) => {
  return ['Completed', 'Cancelled', 'Refunded'].includes(order?.transactionStatus)
}

// ---- 商品详情 ----

const openProductDetail = async (product) => {
  try {
    detailLoading.value = true
    error.value = null
    const res = await getProduct(product.productID)
    selectedProduct.value = res.data
  } catch (e) {
    error.value = '无法加载商品详情: ' + (e.response?.data?.message || e.message)
  } finally {
    detailLoading.value = false
  }
}

const closeProductDetail = () => {
  detailLoading.value = false
  selectedProduct.value = null
}

// ---- 编辑商品 ----

const openEditProduct = (product) => {
  error.value = null
  editingProduct.value = product
}

const closeEditProduct = () => {
  editingProduct.value = null
}

const handleProductSaved = async () => {
  closeEditProduct()
  notice.value = '商品已保存。'
  reloadProductFeeds()
  if (selectedProduct.value) {
    try {
      const refreshed = await getProduct(selectedProduct.value.productID)
      selectedProduct.value = refreshed.data
    } catch (e) {
      error.value = '无法刷新商品详情: ' + (e.response?.data?.message || e.message)
    }
  }
}

// ---- 下单 / 支付 / 确认收货 / 取消订单 ----

const handleCreateOrder = async (product) => {
  try {
    error.value = null
    await createTransaction({ productID: product.productID })
    notice.value = '下单成功，已锁定库存。'
    reloadProductFeeds()
    closeProductDetail()
    return true
  } catch (e) {
    error.value = '下单失败: ' + (e.response?.data?.message || e.message)
    return false
  }
}

const handlePay = async (order) => {
  try {
    error.value = null
    await payTransaction(order.transactionID)
    notice.value = '支付成功。'
    reloadOrderPanel()
  } catch (e) {
    error.value = '支付失败: ' + (e.response?.data?.message || e.message)
  }
}

const handleConfirm = async (order) => {
  try {
    error.value = null
    await confirmReceipt(order.transactionID)
    notice.value = '确认收货成功。'
    reloadOrderPanel()
    reloadProductFeeds()
  } catch (e) {
    error.value = '确认收货失败: ' + (e.response?.data?.message || e.message)
  }
}

const handleCancel = async (order) => {
  try {
    error.value = null
    await cancelTransaction(order.transactionID)
    notice.value = '订单已取消。'
    reloadOrderPanel()
    reloadProductFeeds()
  } catch (e) {
    error.value = '取消订单失败: ' + (e.response?.data?.message || e.message)
  }
}

// ---- 纠纷 ----

const openDispute = (order) => {
  error.value = null
  disputeTarget.value = order
  disputeReason.value = ''
}

const closeDispute = () => {
  disputeTarget.value = null
}

const handleCreateDispute = async () => {
  try {
    error.value = null
    await createDispute(disputeTarget.value.transactionID, { reason: disputeReason.value })
    closeDispute()
    disputeReason.value = ''
    notice.value = '纠纷已提交。'
    reloadOrderPanel()
    reloadSalesPanels()
  } catch (e) {
    error.value = '提交纠纷失败: ' + (e.response?.data?.message || e.message)
  }
}

// ---- 举报 ----

const openReport = (product) => {
  error.value = null
  reportTarget.value = {
    targetType: 'Product',
    typeLabel: '商品',
    id: product.productID,
    title: product.title,
  }
  reportReason.value = ''
  reportDescription.value = ''
}

const closeReport = () => {
  reportTarget.value = null
}

const handleCreateReport = async () => {
  try {
    error.value = null
    await createReport({
      targetType: reportTarget.value.targetType,
      targetID: reportTarget.value.id,
      reason: reportReason.value,
      description: reportDescription.value || undefined,
    })
    closeReport()
    reportReason.value = ''
    reportDescription.value = ''
    notice.value = '举报已提交，我们会尽快核实处理。'
  } catch (e) {
    error.value = '提交举报失败: ' + (e.response?.data?.message || e.message)
  }
}

// ---- 订单留言板 ----

const openOrderMessages = async (order) => {
  disputeTarget.value = null
  reportTarget.value = null
  messageTarget.value = order
  orderMessageText.value = ''
  try {
    error.value = null
    const res = await getOrderMessages(order.transactionID)
    orderMessages.value = res.data
  } catch (e) {
    error.value = '无法加载订单留言: ' + (e.response?.data?.message || e.message)
  }
}

const handleSendOrderMessage = async () => {
  if (!messageTarget.value) return
  if (!orderMessageText.value.trim()) return

  try {
    error.value = null
    await sendOrderMessage(messageTarget.value.transactionID, { content: orderMessageText.value.trim() })
    orderMessageText.value = ''
    await openOrderMessages(messageTarget.value)
    notice.value = '留言已发送。'
    reloadOrderPanel()
    reloadSalesPanels()
  } catch (e) {
    error.value = '发送留言失败: ' + (e.response?.data?.message || e.message)
  }
}

export function useMarket() {
  // useMarket 始终在组件 setup 中调用，可安全获取 router
  const router = useRouter()

  const openSellerHome = (product) => {
    if (product?.userID) {
      router.push(`/user/${product.userID}`)
    }
  }

  return {
    // 数据
    error,
    notice,

    // 面板联动
    registerPanel,
    reloadProductFeeds,

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
    reportDescription,
    openReport,
    closeReport,
    handleCreateReport,

    // 留言板
    messageTarget,
    orderMessages,
    orderMessageText,
    openOrderMessages,
    handleSendOrderMessage,

    // 工具
    statusText,
    formatDate,
    isOrderArchived,
    openSellerHome,
  }
}
