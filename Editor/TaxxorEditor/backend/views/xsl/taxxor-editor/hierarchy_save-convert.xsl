<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:xbrli="http://www.xbrl.org/2003/instance" xmlns:xlink="http://www.w3.org/1999/xlink" xmlns:linkbase="http://www.xbrl.org/2003/linkbase" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:link="http://www.xbrl.org/2003/linkbase" xmlns:lextr="http://lextr.net/alfa/20150401" xmlns:lextr-types="http://lextr.net/types/20150401">
	<xsl:param name="original-hierarchy"/>

	<xsl:variable name="outputchannel-id">
		<xsl:value-of select="/root/data/metadata/hierarchyid"/>
	</xsl:variable>

	<xsl:output method="xml" encoding="UTF-8" indent="yes" media-type="text/xml" />

	<xsl:template match="/root">

		<xsl:variable name="outputchannel-name">
			<xsl:value-of select="$original-hierarchy/hierarchies/output_channel[@id=$outputchannel-id]/name"/>
		</xsl:variable>

		<hierarchy id="{$outputchannel-id}" name="{$outputchannel-name}">

			<xsl:apply-templates select="data[not(metadata)]/id" />
		</hierarchy>
	</xsl:template>


	<xsl:template match="id">
		<xsl:variable name="item-id" select="."/>
		<xsl:variable name="original-item" select="$original-hierarchy/hierarchies/output_channel//item[@id=$item-id]"/>
		<item id="{.}">
			<!-- copy the attributes from the original item into this one -->
			<xsl:apply-templates select="$original-item/@*"/>
			
			<!-- copy the linknames from the original item into this one -->
			<xsl:apply-templates select="$original-item/linknames"/>
			<xsl:if test="count(../children/id) &gt; 0">
				<sub_items>
					<xsl:apply-templates select="../children/id" />
				</sub_items>
			</xsl:if>
		</item>
	</xsl:template>



	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

</xsl:stylesheet>