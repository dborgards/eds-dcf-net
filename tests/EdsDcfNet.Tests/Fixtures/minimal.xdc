<?xml version="1.0" encoding="utf-8"?>
<ISO15745ProfileContainer xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <ISO15745Profile>
    <ProfileHeader>
      <ProfileIdentification>minimal-device-v1</ProfileIdentification>
      <ProfileRevision>1</ProfileRevision>
      <ProfileName>Minimal Device</ProfileName>
      <ProfileSource>Test Vendor</ProfileSource>
      <ProfileClassID>Device</ProfileClassID>
      <ISO15745Reference>
        <ISO15745Part>1</ISO15745Part>
        <ISO15745Edition>1</ISO15745Edition>
        <ProfileTechnology>CANopen</ProfileTechnology>
      </ISO15745Reference>
    </ProfileHeader>
    <ProfileBody xsi:type="ProfileBody_Device_CANopen"
                 fileName="minimal.xdc"
                 fileCreator="EdsDcfNet Test"
                 fileCreationDate="2025-01-15"
                 fileVersion="1">
      <DeviceIdentity>
        <vendorName>Test Vendor</vendorName>
        <vendorID>0x00000100</vendorID>
        <productName>Minimal Device</productName>
        <productID>0x00000001</productID>
      </DeviceIdentity>
      <DeviceManager/>
      <DeviceFunction/>
    </ProfileBody>
  </ISO15745Profile>
  <ISO15745Profile>
    <ProfileHeader>
      <ProfileIdentification>minimal-device-v1-commnet</ProfileIdentification>
      <ProfileRevision>1</ProfileRevision>
      <ProfileName>Minimal Device CommunicationNetwork</ProfileName>
      <ProfileSource>Test Vendor</ProfileSource>
      <ProfileClassID>CommunicationNetwork</ProfileClassID>
      <ISO15745Reference>
        <ISO15745Part>1</ISO15745Part>
        <ISO15745Edition>1</ISO15745Edition>
        <ProfileTechnology>CANopen</ProfileTechnology>
      </ISO15745Reference>
    </ProfileHeader>
    <ProfileBody xsi:type="ProfileBody_CommunicationNetwork_CANopen"
                 fileName="minimal.xdc"
                 fileCreator="EdsDcfNet Test"
                 fileCreationDate="2025-01-15"
                 fileVersion="1">
      <ApplicationLayers>
        <CANopenObjectList mandatoryObjects="2" optionalObjects="0" manufacturerObjects="0">
          <CANopenObject index="1000" name="Device Type" objectType="7" dataType="0007"
                         accessType="ro" defaultValue="0x00000000" PDOmapping="no"
                         actualValue="0x00000191"/>
          <CANopenObject index="1001" name="Error Register" objectType="7" dataType="0005"
                         accessType="ro" defaultValue="0" PDOmapping="no"
                         actualValue="0"/>
        </CANopenObjectList>
      </ApplicationLayers>
      <TransportLayers>
        <PhysicalLayer>
          <baudRate defaultValue="500 Kbps">
            <supportedBaudRate value="250 Kbps"/>
            <supportedBaudRate value="500 Kbps"/>
          </baudRate>
        </PhysicalLayer>
      </TransportLayers>
      <NetworkManagement>
        <CANopenGeneralFeatures granularity="8" nrOfRxPDO="0" nrOfTxPDO="0"
                                bootUpSlave="true" layerSettingServiceSlave="false"
                                groupMessaging="false" dynamicChannels="0"/>
        <CANopenMasterFeatures bootUpMaster="false"/>
        <deviceCommissioning nodeID="5" nodeName="MinimalDevice"
                             actualBaudRate="500 Kbps" networkNumber="1"
                             networkName="CANopen Network" CANopenManager="false"/>
      </NetworkManagement>
    </ProfileBody>
  </ISO15745Profile>
</ISO15745ProfileContainer>
